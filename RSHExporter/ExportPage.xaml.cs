using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    private readonly Dictionary<string, FileFormat> _fileFormatByNames;

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

        _fileFormatByNames = new Dictionary<string, FileFormat>();

        foreach (var fileFormat in Enum.GetValues<FileFormat>())
        {
            // FIXME
            if (fileFormat == FileFormat.Docx)
            {
                continue;
            }

            var displayName = fileFormat.DisplayName();
            FileFormats.Items.Add(displayName);

            _fileFormatByNames[displayName] = fileFormat;
        }

        UpdateDirectoryPath(ExportConfiguration.DirectoryPath);
        FileFormats.SelectedItem = ExportConfiguration.FileFormat.DisplayName();

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
        if (string.IsNullOrWhiteSpace(ExportConfiguration.DirectoryPath))
        {
            ExportFolderContent.Background = Brushes.Yellow;
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.ExportMissingFolder);
            ExportFolderContent.Background = Brushes.White;
            return;
        }

        ToggleExportButtonLoading(true);

        SaveCurrentConfiguration();

        var tasks = new List<Task>();

        foreach (var thread in _threads)
        {
            tasks.Add(PrepareAndExport(thread));
        }

        await Task.WhenAll(tasks);

        ToggleExportButtonLoading(false);

        DialogUtil.ShowInformation(string.Format(RSHExporter.Resources.Localization.Resources.ExportFilesExported,
            tasks.Count));
        Process.Start("explorer.exe", ExportConfiguration.DirectoryPath);
    }

    private void ToggleExportButtonLoading(bool isLoading)
    {
        ExportButton.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        LoadingSpinner.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveCurrentConfiguration()
    {
        ExportConfiguration.FileFormat = _fileFormatByNames[FileFormats.SelectedItem.ToString() ?? ""];
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
}