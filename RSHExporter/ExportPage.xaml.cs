﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using JetBrains.Annotations;
using Ookii.Dialogs.Wpf;
using RSHExporter.Export;
using RSHExporter.Scrape;
using RSHExporter.Utils;

namespace RSHExporter;

public partial class ExportPage : Page
{
    private readonly List<Thread> _threads;

    public ExportPage(List<Thread> threads)
    {
        InitializeComponent();

        SelectedThreads = new ObservableCollection<Thread>();
        foreach (var thread in threads)
        {
            SelectedThreads.Add(thread);
        }

        SelectedThreadItems.DataContext = SelectedThreads;

        _threads = threads;

        SelectableFileFormats = new ObservableCollection<SelectableFileFormat>();
        foreach (var fileFormat in Enum.GetValues<FileFormat>())
        {
            // FIXME
            SelectableFileFormats.Add(
                new SelectableFileFormat(fileFormat, ExportConfiguration.FileFormats.Contains(fileFormat)));
        }

        FileFormats.DataContext = SelectableFileFormats;

        UpdateDirectoryPath(ExportConfiguration.DirectoryPath);

        if (ExportConfiguration.IncludeGroup)
        {
            IncludeGroupCheckBox.IsChecked = true;
            ToggleGroupCheckBoxes(true);
            IncludeGroupAuthorCheckBox.IsChecked = ExportConfiguration.IncludeGroupAuthor;
            IncludeGroupPostedAtCheckBox.IsChecked = ExportConfiguration.IncludeGroupPostedAt;
            IncludeGroupUrlCheckBox.IsChecked = ExportConfiguration.IncludeGroupUrl;
        }
        else
        {
            IncludeGroupCheckBox.IsChecked = false;
            ToggleGroupCheckBoxes(false);
        }

        if (ExportConfiguration.IncludeThread)
        {
            IncludeThreadCheckBox.IsChecked = true;
            ToggleThreadCheckBoxes(true);
            IncludeThreadAuthorCheckBox.IsChecked = ExportConfiguration.IncludeThreadAuthor;
            IncludeThreadPostedAtCheckBox.IsChecked = ExportConfiguration.IncludeThreadPostedAt;
            IncludeThreadUrlCheckBox.IsChecked = ExportConfiguration.IncludeThreadUrl;
        }
        else
        {
            IncludeThreadCheckBox.IsChecked = false;
            ToggleThreadCheckBoxes(false);
        }

        IncludePostAuthorCheckBox.IsChecked = ExportConfiguration.IncludePostAuthor;
        IncludePostPostedAtCheckBox.IsChecked = ExportConfiguration.IncludePostPostedAt;

        IncludePostNumberCheckBox.IsChecked = ExportConfiguration.IncludePostNumber;
        IncludePageNumberCheckBox.IsChecked = ExportConfiguration.IncludePageNumber;

        DownloadToOwnFolderCheckBox.IsChecked = ExportConfiguration.DownloadToOwnFolder;

        if (ExportConfiguration.IncludeImages)
        {
            IncludeImagesCheckBox.IsChecked = true;
            ToggleImageCheckBoxes(true);

            if (ExportConfiguration.DownloadImages)
            {
                DownloadImagesCheckBox.IsChecked = true;
                ToggleDownloadImageCheckBoxes(true);

                DownloadImagesToOwnFolderCheckBox.IsChecked = ExportConfiguration.DownloadImagesToOwnFolder;
            }
            else
            {
                ToggleDownloadImageCheckBoxes(false);
                DownloadImagesCheckBox.IsChecked = false;
            }
        }
        else
        {
            IncludeImagesCheckBox.IsChecked = false;
            ToggleImageCheckBoxes(false);
        }

        ReserveOrderCheckBox.IsChecked = ExportConfiguration.ReserveOrder;
    }

    [UsedImplicitly] public ObservableCollection<Thread> SelectedThreads { get; set; }
    [UsedImplicitly] public ObservableCollection<SelectableFileFormat> SelectableFileFormats { get; set; }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentConfiguration();
        NavigationService.GoBack();
    }

    private void ChooseExportDirectoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        AskForDirectoryPath();
    }

    private void AskForDirectoryPath()
    {
        while (true)
        {
            var browserDialog = new VistaFolderBrowserDialog();
            browserDialog.ShowDialog();

            var path = browserDialog.SelectedPath;

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length > 0)
            {
                if (!DialogUtil.ShowQuestion(RSHExporter.Resources.Localization.Resources.ExportFolderContainsFiles))
                {
                    continue;
                }
            }

            ExportConfiguration.DirectoryPath = path;
            UpdateDirectoryPath(path);
            break;
        }
    }

    private void UpdateDirectoryPath(string? path)
    {
        ExportDirectoryHyperlink.IsEnabled = path != null;
        ExportDirectoryTextBlock.Text = path ?? "/";
    }

    private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleExportButtonLoading(true);

        if (string.IsNullOrWhiteSpace(ExportConfiguration.DirectoryPath))
        {
            ToggleExportButtonLoading(false);

            ExportFolderContent.Background = Brushes.Yellow;
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.ExportMissingFolder);
            ExportFolderContent.Background = Brushes.White;
            return;
        }

        SaveCurrentConfiguration();

        if (ExportConfiguration.FileFormats.Count == 0)
        {
            ToggleExportButtonLoading(false);

            ExportFormatContent.Background = Brushes.Yellow;
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.ExportMissingFileFormat);
            ExportFormatContent.Background = Brushes.White;
            return;
        }

        var tasks = new List<Task>();

        foreach (var thread in _threads)
        {
            tasks.Add(PrepareAndExport(thread));
        }

        await Task.WhenAll(tasks);

        ToggleExportButtonLoading(false);

        var numberFilesExported = tasks.Count * ExportConfiguration.FileFormats.Count;

        DialogUtil.ShowInformation(numberFilesExported == 1
            ? RSHExporter.Resources.Localization.Resources.ExportFileExported
            : string.Format(RSHExporter.Resources.Localization.Resources.ExportFilesExported, numberFilesExported)
        );

        Process.Start("explorer.exe", ExportConfiguration.DirectoryPath);
    }

    private void ToggleExportButtonLoading(bool isLoading)
    {
        ExportButton.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        LoadingSpinner.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveCurrentConfiguration()
    {
        ExportConfiguration.FileFormats = GetSelectedFileFormats();
        ExportConfiguration.IncludeGroup = IncludeGroupCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeGroupAuthor = IncludeGroupAuthorCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeGroupPostedAt = IncludeGroupPostedAtCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeGroupUrl = IncludeGroupUrlCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeThread = IncludeThreadCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeThreadAuthor = IncludeThreadAuthorCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeThreadPostedAt = IncludeThreadPostedAtCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeThreadUrl = IncludeThreadUrlCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludePostAuthor = IncludePostAuthorCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludePostPostedAt = IncludePostPostedAtCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludePostNumber = IncludePostNumberCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludePageNumber = IncludePageNumberCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.DownloadToOwnFolder = DownloadToOwnFolderCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.IncludeImages = IncludeImagesCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.DownloadImages = DownloadImagesCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.DownloadImagesToOwnFolder = DownloadImagesToOwnFolderCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.ReserveOrder = ReserveOrderCheckBox.IsChecked.GetValueOrDefault();
    }

    private List<FileFormat> GetSelectedFileFormats()
    {
        return (from selectableFileFormat in SelectableFileFormats
            where selectableFileFormat.IsSelected
            select selectableFileFormat.FileFormat).ToList();
    }

    private static async Task PrepareAndExport(Thread thread)
    {
        var posts = await Scraper.GetPosts(thread);
        await Exporter.Export(posts);
    }

    private void IncludeGroupCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        ToggleGroupCheckBoxes(true);
    }

    private void IncludeGroupCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ToggleGroupCheckBoxes(false);
    }

    private void ToggleGroupCheckBoxes(bool toggle)
    {
        IncludeGroupAuthorCheckBox.IsEnabled = toggle;
        IncludeGroupPostedAtCheckBox.IsEnabled = toggle;
        IncludeGroupUrlCheckBox.IsEnabled = toggle;

        if (!toggle)
        {
            IncludeGroupAuthorCheckBox.IsChecked = false;
            IncludeGroupPostedAtCheckBox.IsChecked = false;
            IncludeGroupUrlCheckBox.IsChecked = false;
        }
    }

    private void IncludeThreadCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        ToggleThreadCheckBoxes(true);
    }

    private void IncludeThreadCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ToggleThreadCheckBoxes(false);
    }

    private void ToggleThreadCheckBoxes(bool toggle)
    {
        IncludeThreadAuthorCheckBox.IsEnabled = toggle;
        IncludeThreadPostedAtCheckBox.IsEnabled = toggle;
        IncludeThreadUrlCheckBox.IsEnabled = toggle;

        if (!toggle)
        {
            IncludeThreadAuthorCheckBox.IsChecked = false;
            IncludeThreadPostedAtCheckBox.IsChecked = false;
            IncludeThreadUrlCheckBox.IsChecked = false;
        }
    }

    private void IncludeImagesCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        ToggleImageCheckBoxes(true);
    }

    private void IncludeImagesCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ToggleImageCheckBoxes(false);
    }

    private void ToggleImageCheckBoxes(bool toggle)
    {
        DownloadImagesCheckBox.IsEnabled = toggle;

        if (!toggle)
        {
            DownloadImagesCheckBox.IsChecked = false;
            ToggleDownloadImageCheckBoxes(false);
        }
    }

    private void DownloadImagesCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        ToggleDownloadImageCheckBoxes(true);
    }

    private void DownloadImagesCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        ToggleDownloadImageCheckBoxes(false);
    }

    private void ToggleDownloadImageCheckBoxes(bool toggle)
    {
        DownloadImagesToOwnFolderCheckBox.IsEnabled = toggle;

        if (!toggle)
        {
            DownloadImagesToOwnFolderCheckBox.IsChecked = false;
        }
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogUtil.ShowHelpAndHighlight(
            (brush => SelectedContent.Background = brush,
                "First check whether you have selected all groups and threads you want to export."),
            (brush => GoBackContent.Background = brush,
                "You can always go back to selecting groups and threads. All of your changes are automatically saved."),
            (brush => ExportFolderContent.Background = brush,
                "Start the export process by choosing the folder the files should be saved in. Files will be overwritten, if they already exist."),
            (brush => ExportFormatContent.Background = brush,
                "Then choose the format of the exported files."),
            (brush => ExportOptionsContent.Background = brush,
                "Configure how you want the groups and threads to be exported."),
            (brush => StartExportContent.Background = brush,
                "Finally start the export here. (Exporting can take a while, especially with a slow internet connection.)")
        );
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        FeedbackUtil.HandleFeedback("ExportPage");
    }

    private void DirectoryPath_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (ExportConfiguration.DirectoryPath != null)
        {
            Process.Start("explorer.exe", ExportConfiguration.DirectoryPath);
        }

        e.Handled = true;
    }

    public sealed class SelectableFileFormat : INotifyPropertyChanged
    {
        private FileFormat _fileFormat;
        private string _icon;
        private bool _isSelected;
        private string _label;

        public SelectableFileFormat(FileFormat fileFormat, bool isSelected = false)
        {
            _fileFormat = fileFormat;
            _label = fileFormat.DisplayName();
            _icon = fileFormat.FontAwesomeIcon();
            _isSelected = isSelected;
        }

        [UsedImplicitly]
        public FileFormat FileFormat
        {
            get => _fileFormat;
            set => SetField(ref _fileFormat, value);
        }

        [UsedImplicitly]
        public string Label
        {
            get => _label;
            set => SetField(ref _label, value);
        }

        [UsedImplicitly]
        public string Icon
        {
            get => _icon;
            set => SetField(ref _icon, value);
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