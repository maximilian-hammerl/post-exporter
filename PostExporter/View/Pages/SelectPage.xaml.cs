﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;
using PostExporter.Scrape;
using PostExporter.Utils;
using Sentry;

namespace PostExporter.View.Pages;

public partial class SelectPage : Page
{
    private readonly Dictionary<int, SelectableGroup> _selectableGroupByIds = new();
    private SelectableGroup? _currentSelectableGroup;

    public SelectPage(IEnumerable<Group> groups)
    {
        InitializeComponent();

        SelectableGroups = [];
        GroupItems.DataContext = SelectableGroups;

        SelectableThreads = [];
        ThreadItems.DataContext = SelectableThreads;

        foreach (var group in groups)
        {
            var selectableGroup = new SelectableGroup(group);
            _selectableGroupByIds[group.Id] = selectableGroup;
            SelectableGroups.Add(selectableGroup);
        }

        SentryUtil.HandleBreadcrumb(
            "Opened page",
            "SelectPage",
            level: BreadcrumbLevel.Info
        );
    }

    [UsedImplicitly] public ObservableCollection<SelectableGroup> SelectableGroups { get; set; }
    [UsedImplicitly] public ObservableCollection<SelectableThread> SelectableThreads { get; set; }

    private async void GroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            throw new ArgumentException(sender.ToString());
        }

        var selectableGroup = _selectableGroupByIds[(int)button.Tag];
        await UpdateGroupSelected(selectableGroup);
    }

    private async void GroupCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            throw new ArgumentException(sender.ToString());
        }

        var selectableGroup = _selectableGroupByIds[(int)checkBox.Tag];

        await UpdateGroupSelected(selectableGroup);
    }

    private async Task UpdateGroupSelected(SelectableGroup selectableGroup, bool updateGroupSelected = true,
        bool? updateThreadSelected = null)
    {
        if (selectableGroup.IsActive)
        {
            return;
        }

        selectableGroup.IsActive = true;
        selectableGroup.IsLoading = true;

        var selectableThreads = await selectableGroup.GetOrLoadSelectableThreads();

        if (selectableThreads.Count == 0)
        {
            selectableGroup.IsLoading = false;
            selectableGroup.IsActive = false;
            selectableGroup.IsSelected = false;
            selectableGroup.IsEnabled = false;
            return;
        }

        if (updateThreadSelected != null)
        {
            foreach (var selectableThread in selectableThreads)
            {
                selectableThread.IsSelected = updateThreadSelected.Value;
            }
        }

        if (_currentSelectableGroup != null)
        {
            _currentSelectableGroup.IsActive = false;
        }

        _currentSelectableGroup = selectableGroup;

        selectableGroup.IsSelected = updateGroupSelected;
        selectableGroup.IsLoading = false;

        ThreadLabel.Text = string.Format(PostExporter.Resources.Localization.Resources.SelectThreadsOf,
            selectableGroup.Group.Title);

        SelectableThreads.Clear();

        foreach (var selectableThread in selectableThreads)
        {
            SelectableThreads.Add(selectableThread);
        }
    }

    private void LogoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleBreadcrumb(
            "Logged out",
            "SelectPage",
            level: BreadcrumbLevel.Info
        );

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
            SentryUtil.HandleBreadcrumb(
                "Nothing selected",
                "SelectPage",
                level: BreadcrumbLevel.Warning
            );

            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningNothingSelected);
            return;
        }

        SentryUtil.HandleBreadcrumb(
            $"To export with {threads.Count} threads",
            "SelectPage",
            level: BreadcrumbLevel.Info
        );

        NavigationService.Navigate(new ExportPage(threads.AsReadOnly()));
    }

    private async void SelectAllGroups_OnClick(object sender, RoutedEventArgs e)
    {
        await ToggleSelectableGroupsSelected(true);
    }

    private async void UnselectAllGroups_OnClick(object sender, RoutedEventArgs e)
    {
        await ToggleSelectableGroupsSelected(false);
    }

    private async Task ToggleSelectableGroupsSelected(bool toggle)
    {
        foreach (var selectableGroup in SelectableGroups)
        {
            if (!selectableGroup.IsEnabled)
            {
                continue;
            }

            await UpdateGroupSelected(selectableGroup, toggle);
        }
    }

    private async void SelectAllGroupsThreads_OnClick(object sender, RoutedEventArgs e)
    {
        await ToggleSelectableGroupsThreadsSelected(true);
    }

    private async void UnselectAllGroupsThreads_OnClick(object sender, RoutedEventArgs e)
    {
        await ToggleSelectableGroupsThreadsSelected(false);
    }

    private async Task ToggleSelectableGroupsThreadsSelected(bool toggle)
    {
        foreach (var selectableGroup in SelectableGroups)
        {
            if (!selectableGroup.IsEnabled)
            {
                continue;
            }

            await UpdateGroupSelected(selectableGroup, toggle, toggle);
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
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningSelectGroupFirst);
            return;
        }

        foreach (var selectableThread in await _currentSelectableGroup.GetOrLoadSelectableThreads())
        {
            selectableThread.IsSelected = toggle;
        }
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleBreadcrumb(
            "Opened help",
            "SelectPage",
            level: BreadcrumbLevel.Info
        );

        DialogUtil.ShowHelpAndHighlight(
            (brush => GroupsContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpSelectStep1),
            (brush => ThreadsContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpSelectStep2),
            (brush =>
                {
                    GroupButtonsContent.Background = brush;
                    ThreadButtonsContent.Background = brush;
                },
                PostExporter.Resources.Localization.Resources.HelpSelectStep3),
            (brush =>
                {
                    GroupsContent.Background = brush;
                    ThreadsContent.Background = brush;
                },
                PostExporter.Resources.Localization.Resources.HelpSelectStep4),
            (brush => ToExportContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpSelectStep5)
        );
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleFeedback("SelectPage");
    }

    private void UrlButton_OnClick(object sender, RoutedEventArgs e)
    {
        Util.OpenBrowser((string)((Button)sender).Tag);
    }

    public sealed class SelectableGroup : INotifyPropertyChanged
    {
        private Group _group;
        private bool _isActive;
        private bool _isEnabled;
        private bool _isLoading;

        private bool _isSelected;

        private List<SelectableThread>? _selectableThreads;

        public SelectableGroup(Group group)
        {
            _group = group;
            _isSelected = false;
            _isActive = false;
            _isLoading = false;
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
        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
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

            _selectableThreads = [];

            var (threads, loadedAllThreadsSuccessfully) = await Scraper.GetThreads(Group);

            if (!loadedAllThreadsSuccessfully)
            {
                DialogUtil.ShowError(
                    string.Format(PostExporter.Resources.Localization.Resources.ErrorSomeThreadsFailedToLoad,
                        Group.Title), true);
            }

            if (threads.Count == 0)
            {
                DialogUtil.ShowWarning(string.Format(PostExporter.Resources.Localization.Resources.WarningNoThreadsFor,
                    Group.Title));
            }
            else
            {
                foreach (var thread in threads)
                {
                    _selectableThreads.Add(new SelectableThread(thread));
                }
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