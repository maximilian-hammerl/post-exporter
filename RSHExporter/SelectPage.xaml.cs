using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;
using RSHExporter.Scrape;
using RSHExporter.Utils;

namespace RSHExporter;

public partial class SelectPage : Page
{
    private readonly Dictionary<string, SelectableGroup> _selectableGroupByTitles = new();
    private SelectableGroup? _currentSelectableGroup;

    public SelectPage(IEnumerable<Group> groups)
    {
        InitializeComponent();

        SelectableGroups = new ObservableCollection<SelectableGroup>();
        GroupItems.DataContext = SelectableGroups;

        SelectableThreads = new ObservableCollection<SelectableThread>();
        ThreadItems.DataContext = SelectableThreads;

        foreach (var group in groups)
        {
            var selectableGroup = new SelectableGroup(group);
            _selectableGroupByTitles[group.Title] = selectableGroup;
            SelectableGroups.Add(selectableGroup);
        }
    }

    [UsedImplicitly] public ObservableCollection<SelectableGroup> SelectableGroups { get; set; }
    [UsedImplicitly] public ObservableCollection<SelectableThread> SelectableThreads { get; set; }

    private async void GroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            throw new ArgumentException(sender.ToString());
        }

        var title = button.Content.ToString() ?? "";
        var selectableGroup = _selectableGroupByTitles[title];

        var selectableThreads = await selectableGroup.GetOrLoadSelectableThreads();

        if (selectableThreads.Count == 0)
        {
            selectableGroup.IsSelected = false;
            selectableGroup.IsEnabled = false;
            DialogUtil.ShowWarning(string.Format(RSHExporter.Resources.Localization.Resources.SelectNoThreadsFor,
                selectableGroup.Group));
            return;
        }

        if (_currentSelectableGroup != null)
        {
            _currentSelectableGroup.IsActive = false;
        }

        _currentSelectableGroup = selectableGroup;

        selectableGroup.IsSelected = true;
        selectableGroup.IsActive = true;

        ThreadLabel.Text = string.Format(RSHExporter.Resources.Localization.Resources.SelectThreadsOf, title);

        SelectableThreads.Clear();

        foreach (var selectableThread in selectableThreads)
        {
            SelectableThreads.Add(selectableThread);
        }
    }

    private void LogoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationService.Navigate(new LoginPage());
    }

    private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        var threads = new List<Thread>();

        foreach (var selectableGroup in SelectableGroups)
        {
            if (!selectableGroup.IsEnabled || !selectableGroup.IsSelected)
            {
                continue;
            }

            foreach (var selectableThread in await selectableGroup.GetOrLoadSelectableThreads())
            {
                if (!selectableThread.IsSelected)
                {
                    continue;
                }

                threads.Add(selectableThread.Thread);
            }
        }

        if (threads.Count == 0)
        {
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.SelectNothingSelected);
            return;
        }

        NavigationService.Navigate(new ExportPage(threads));
    }

    private void SelectAllGroups_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleSelectableGroupsSelected(true);
    }

    private void UnselectAllGroups_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleSelectableGroupsSelected(false);
    }

    private void ToggleSelectableGroupsSelected(bool toggle)
    {
        foreach (var selectableGroup in SelectableGroups)
        {
            if (!selectableGroup.IsEnabled)
            {
                continue;
            }

            selectableGroup.IsSelected = toggle;
        }
    }

    private async void SelectAllThreadsButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ToggleSelectableThreadsSelected(true);
    }

    private async void UnselectAllThreads_OnClick(object sender, RoutedEventArgs e)
    {
        await ToggleSelectableThreadsSelected(false);
    }

    private async Task ToggleSelectableThreadsSelected(bool toggle)
    {
        if (_currentSelectableGroup == null)
        {
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.SelectGroupFirst);
            return;
        }

        foreach (var selectableThread in await _currentSelectableGroup.GetOrLoadSelectableThreads())
        {
            selectableThread.IsSelected = toggle;
        }
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogUtil.ShowHelpAndHighlight(
            (brush => GroupsContent.Background = brush,
                "Start by selecting groups. Clicking on the name of the group will load all threads of it."),
            (brush => ThreadsContent.Background = brush,
                "For every group select the threads you want to export next."),
            (brush => GroupButtonsContent.Background = brush,
                "You can select und unselect all groups here..."),
            (brush => ThreadButtonsContent.Background = brush,
                "...and all threads here."),
            (brush =>
                {
                    GroupsContent.Background = brush;
                    ThreadsContent.Background = brush;
                },
                "You can open a group or thread in the browser by clicking on the small icon to the right of the group or thread name."),
            (brush => ToExportContent.Background = brush,
                "If you have selected all groups and threads, continue to export by clicking here. (You can also go back without losing any of your changes.)")
        );
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        FeedbackUtil.HandleFeedback("ExportPage");
    }

    private void UrlButton_OnClick(object sender, RoutedEventArgs e)
    {
        var url = (string)((Button)sender).Tag;

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(url)
        {
            UseShellExecute = true
        };
        process.Start();
    }

    public sealed class SelectableGroup : INotifyPropertyChanged
    {
        private Group _group;
        private bool _isActive;
        private bool _isEnabled;
        private bool _isSelected;
        private List<SelectableThread>? _selectableThreads;

        public SelectableGroup(Group group)
        {
            _group = group;
            _isSelected = false;
            _isActive = false;
            _isEnabled = true;
            _selectableThreads = null;
        }

        [UsedImplicitly]
        public Group Group
        {
            get => _group;
            set => SetField(ref _group, value);
        }

        [UsedImplicitly]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        [UsedImplicitly]
        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        [UsedImplicitly]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetField(ref _isEnabled, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public async Task<List<SelectableThread>> GetOrLoadSelectableThreads()
        {
            if (_selectableThreads != null)
            {
                return _selectableThreads;
            }

            _selectableThreads = new List<SelectableThread>();

            var threads = await Scraper.GetThreads(Group);
            foreach (var thread in threads)
            {
                _selectableThreads.Add(new SelectableThread(thread));
            }

            return _selectableThreads;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }
    }

    public sealed class SelectableThread : INotifyPropertyChanged
    {
        private bool _isSelected;

        private Thread _thread;

        public SelectableThread(Thread thread)
        {
            _thread = thread;
            _isSelected = false;
        }

        [UsedImplicitly]
        public Thread Thread
        {
            get => _thread;
            set => SetField(ref _thread, value);
        }

        [UsedImplicitly]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged(propertyName);
        }
    }
}