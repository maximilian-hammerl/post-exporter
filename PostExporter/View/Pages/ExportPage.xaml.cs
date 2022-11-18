using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using JetBrains.Annotations;
using Ookii.Dialogs.Wpf;
using PostExporter.Export;
using PostExporter.Scrape;
using PostExporter.Utils;
using Sentry;
using Thread = PostExporter.Scrape.Thread;

namespace PostExporter.View.Pages;

public partial class ExportPage : Page
{
    private readonly List<Thread> _threads;
    private CancellationTokenSource _cancellationTokenSource = new();

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
            SelectableFileFormats.Add(new SelectableFileFormat(fileFormat,
                ExportConfiguration.FileFormats.Contains(fileFormat)));
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
        IncludeImagesCheckBox.IsChecked = ExportConfiguration.IncludeImages;
        ReserveOrderCheckBox.IsChecked = ExportConfiguration.ReserveOrder;

        UseCustomTemplatesCheckBox.IsChecked = ExportConfiguration.UseCustomTemplates;

        DownloadToOwnFolderCheckBox.IsChecked = ExportConfiguration.DownloadToOwnFolder;

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

        SentryUtil.HandleBreadcrumb(
            "Opened page",
            "ExportPage",
            level: BreadcrumbLevel.Info
        );
    }

    [UsedImplicitly] public ObservableCollection<Thread> SelectedThreads { get; set; }
    [UsedImplicitly] public ObservableCollection<SelectableFileFormat> SelectableFileFormats { get; set; }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleBreadcrumb(
            "Back to selecting groups and threads",
            "ExportPage",
            level: BreadcrumbLevel.Info
        );

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

            try
            {
                if (Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length > 0)
                {
                    if (!DialogUtil.ShowQuestion(PostExporter.Resources.Localization.Resources
                            .ExportFolderContainsFilesQuestion))
                    {
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                DialogUtil.ShowError(PostExporter.Resources.Localization.Resources.ErrorUnauthorizedAccess, false);
                return;
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

    private void FileFormatCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            throw new ArgumentException(sender.ToString());
        }

        if (!checkBox.IsChecked.HasValue || !checkBox.IsChecked.Value)
        {
            return;
        }

        var fileFormat = Enum.Parse<FileFormat>(checkBox.Tag.ToString() ?? "");

        if (fileFormat != FileFormat.Docx)
        {
            return;
        }

        DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningSelectedDocx);
    }

    private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleExportButtonLoading(true);

        SentryUtil.HandleBreadcrumb(
            "Started export",
            "ExportPage",
            level: BreadcrumbLevel.Info
        );

        SaveCurrentConfiguration();

        LoadingProgressBar.Value = 0;
        LoadingProgressBar.Maximum = _threads.Count;

        if (string.IsNullOrWhiteSpace(ExportConfiguration.DirectoryPath))
        {
            SentryUtil.HandleBreadcrumb(
                "Missing folder",
                "ExportPage",
                level: BreadcrumbLevel.Warning
            );

            ToggleExportButtonLoading(false);

            ExportFolderContent.Background = Brushes.LightBlue;
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningMissingFolder);
            ExportFolderContent.Background = Brushes.White;
            return;
        }

        if (ExportConfiguration.FileFormats.Count == 0)
        {
            SentryUtil.HandleBreadcrumb(
                "Missing file format",
                "ExportPage",
                level: BreadcrumbLevel.Warning
            );

            ToggleExportButtonLoading(false);

            ExportFormatContent.Background = Brushes.LightBlue;
            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningMissingFileFormat);
            ExportFormatContent.Background = Brushes.White;
            return;
        }

        StringBuilder textHeadTemplate;
        StringBuilder textBodyTemplate;
        if (ExportConfiguration.UseCustomTemplates && ExportConfiguration.FileFormats.Contains(FileFormat.Txt) &&
            ExportConfiguration.TextHeadTemplate != null && ExportConfiguration.TextBodyTemplate != null)
        {
            if (!ValidateTemplate(ExportConfiguration.TextHeadTemplate, ExportConfiguration.TextBodyTemplate,
                    out var missingPlaceholders, out var unusedPlaceholders))
            {
                SentryUtil.HandleBreadcrumb(
                    "Html template not complete",
                    "ExportPage",
                    level: BreadcrumbLevel.Warning
                );

                ShowErrorMessageForTemplate("Html", missingPlaceholders, unusedPlaceholders);
                ToggleExportButtonLoading(false);
                return;
            }

            textHeadTemplate = ExportConfiguration.TextHeadTemplate;
            textBodyTemplate = ExportConfiguration.TextBodyTemplate;
        }
        else
        {
            textHeadTemplate = ExportConfiguration.GetDefaultTextHeadTemplate();
            textBodyTemplate = ExportConfiguration.GetDefaultTextBodyTemplate();
        }

        StringBuilder htmlHeadTemplate;
        StringBuilder htmlBodyTemplate;
        if (ExportConfiguration.UseCustomTemplates &&
            (ExportConfiguration.FileFormats.Contains(FileFormat.Html) ||
             ExportConfiguration.FileFormats.Contains(FileFormat.Docx)) &&
            ExportConfiguration.HtmlHeadTemplate != null && ExportConfiguration.HtmlBodyTemplate != null)
        {
            if (!ValidateTemplate(ExportConfiguration.HtmlHeadTemplate, ExportConfiguration.HtmlBodyTemplate,
                    out var missingPlaceholders, out var unusedPlaceholders))
            {
                SentryUtil.HandleBreadcrumb(
                    "Text template not complete",
                    "ExportPage",
                    level: BreadcrumbLevel.Warning
                );

                ShowErrorMessageForTemplate("Text", missingPlaceholders, unusedPlaceholders);
                ToggleExportButtonLoading(false);
                return;
            }

            htmlHeadTemplate = ExportConfiguration.HtmlHeadTemplate;
            htmlBodyTemplate = ExportConfiguration.HtmlBodyTemplate;
        }
        else
        {
            htmlHeadTemplate = ExportConfiguration.GetDefaultHtmlHeadTemplate();
            htmlBodyTemplate = ExportConfiguration.GetDefaultHtmlBodyTemplate();
        }

        var failedExports = new ConcurrentDictionary<ExportError, ConcurrentBag<Thread>>();

        foreach (var exportError in Enum.GetValues<ExportError>())
        {
            failedExports[exportError] = new ConcurrentBag<Thread>();
        }

        _cancellationTokenSource = new CancellationTokenSource();

        await Task.WhenAll(_threads.Select(async thread =>
        {
            try
            {
                await PrepareAndExport(thread, textHeadTemplate, textBodyTemplate, htmlHeadTemplate, htmlBodyTemplate,
                    _cancellationTokenSource.Token);
            }
            catch (UnauthorizedAccessException exception)
            {
                failedExports[ExportError.DirectoryAccess].Add(thread);
                SentryUtil.HandleMessage($"UnauthorizedAccessException {exception} for {thread.Title} ({thread.Url})");
            }
            catch (EndOfStreamException exception)
            {
                failedExports[ExportError.WordImageDownload].Add(thread);
                SentryUtil.HandleMessage($"EndOfStreamException {exception} for {thread.Title} ({thread.Url})");
            }
            catch (InvalidOperationException exception)
            {
                failedExports[ExportError.WordImageDownload].Add(thread);
                SentryUtil.HandleMessage($"InvalidOperationException {exception} for {thread.Title} ({thread.Url})");
            }
            catch (HttpRequestException exception)
            {
                failedExports[ExportError.Connection].Add(thread);
                SentryUtil.HandleMessage($"HttpRequestException {exception} for {thread.Title} ({thread.Url})");
            }
            catch (Exception exception)
            {
                failedExports[ExportError.Unrecognized].Add(thread);
                SentryUtil.HandleMessage($"Exception {exception} for {thread.Title} ({thread.Url})");
            }
        }));

        ToggleExportButtonLoading(false);

        var numberFailedExports = failedExports.Values.Sum(threads => threads.Count);

        if (numberFailedExports > 0)
        {
            SentryUtil.HandleBreadcrumb(
                $"{numberFailedExports} of {_threads.Count} exports failed",
                "ExportPage",
                level: BreadcrumbLevel.Error
            );

            foreach (var (exportError, threads) in failedExports)
            {
                if (!threads.IsEmpty)
                {
                    SentryUtil.HandleBreadcrumb(
                        $"{threads.Count} of {_threads.Count} exports failed because of {exportError}",
                        "ExportPage",
                        level: BreadcrumbLevel.Error
                    );

                    var threadTitles = string.Join(", ",
                        threads.Select(thread => $"{thread.Title} ({thread.Group.Title})"));

                    switch (exportError)
                    {
                        case ExportError.DirectoryAccess:
                            DialogUtil.ShowError(
                                string.Format(
                                    PostExporter.Resources.Localization.Resources.ErrorExportFailedDirectoryAccess,
                                    threadTitles), false);
                            break;
                        case ExportError.Connection:
                            DialogUtil.ShowError(
                                string.Format(PostExporter.Resources.Localization.Resources.ErrorExportFailedConnection,
                                    threadTitles), false);
                            break;
                        case ExportError.WordImageDownload:
                            DialogUtil.ShowError(
                                string.Format(
                                    PostExporter.Resources.Localization.Resources.ErrorExportFailedWordImageDownload,
                                    threadTitles), false);
                            break;
                        case ExportError.Unrecognized:
                            DialogUtil.ShowError(
                                string.Format(
                                    PostExporter.Resources.Localization.Resources.ErrorExportFailedUnrecognized,
                                    threadTitles), true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(exportError), exportError, @"Unknown value");
                    }
                }
            }
        }
        else if (_cancellationTokenSource.IsCancellationRequested)
        {
            SentryUtil.HandleBreadcrumb(
                $"Export of {_threads.Count} threads was cancelled",
                "ExportPage",
                level: BreadcrumbLevel.Warning
            );

            DialogUtil.ShowWarning(PostExporter.Resources.Localization.Resources.WarningExportCancelled);
        }
        else
        {
            var numberFilesExported = _threads.Count * ExportConfiguration.FileFormats.Count;

            SentryUtil.HandleBreadcrumb(
                $"Successfully exported {numberFilesExported} files",
                "ExportPage",
                level: BreadcrumbLevel.Info
            );

            DialogUtil.ShowInformation(numberFilesExported == 1
                ? PostExporter.Resources.Localization.Resources.InfoFileSuccessfullyExported
                : string.Format(PostExporter.Resources.Localization.Resources.InfoFilesSuccessfullyExported,
                    numberFilesExported)
            );
        }

        Process.Start("explorer.exe", ExportConfiguration.DirectoryPath);
    }

    private static void ShowErrorMessageForTemplate(string templateType, List<string> missingPlaceholders,
        List<string> unusedPlaceholder)
    {
        var errorMessage = string.Format(PostExporter.Resources.Localization.Resources.ErrorTemplates, templateType);

        var placeHolderErrorMessage = new List<string>(2);

        switch (missingPlaceholders.Count)
        {
            case 1:
                placeHolderErrorMessage.Add(string.Format(
                    PostExporter.Resources.Localization.Resources.ErrorMissingPlaceholder, missingPlaceholders[0])
                );
                break;
            case > 1:
                placeHolderErrorMessage.Add(string.Format(
                    PostExporter.Resources.Localization.Resources.ErrorMissingPlaceholders,
                    string.Join(", ", missingPlaceholders))
                );
                break;
        }

        switch (unusedPlaceholder.Count)
        {
            case 1:
                placeHolderErrorMessage.Add(string.Format(
                    PostExporter.Resources.Localization.Resources.ErrorUnusedPlaceholder, unusedPlaceholder[0])
                );
                break;
            case > 1:
                placeHolderErrorMessage.Add(string.Format(
                    PostExporter.Resources.Localization.Resources.ErrorUnusedPlaceholders,
                    string.Join(", ", unusedPlaceholder))
                );
                break;
        }

        errorMessage +=
            $" {Util.CapitalizeFirstChar(string.Join($" {PostExporter.Resources.Localization.Resources.And} ", placeHolderErrorMessage))}.";

        DialogUtil.ShowError(errorMessage, false);
    }

    private static bool ValidateTemplate(StringBuilder headTemplate, StringBuilder bodyTemplate,
        out List<string> missingPlaceholders, out List<string> unusedPlaceholders)
    {
        missingPlaceholders = new List<string>();
        unusedPlaceholders = new List<string>();

        var headString = headTemplate.ToString();
        var bodyString = bodyTemplate.ToString();

        // Head template
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString, ExportConfiguration.IncludeGroup,
            PostExporter.Resources.Localization.Resources.PlaceholderGroupTitle);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeGroup && ExportConfiguration.IncludeGroupAuthor,
            PostExporter.Resources.Localization.Resources.PlaceholderGroupAuthor);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeGroup && ExportConfiguration.IncludeGroupPostedAt,
            PostExporter.Resources.Localization.Resources.PlaceholderGroupPostedAt);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeGroup && ExportConfiguration.IncludeGroupUrl,
            PostExporter.Resources.Localization.Resources.PlaceholderGroupUrl);

        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString, ExportConfiguration.IncludeThread,
            PostExporter.Resources.Localization.Resources.PlaceholderThreadTitle);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeThread && ExportConfiguration.IncludeThreadAuthor,
            PostExporter.Resources.Localization.Resources.PlaceholderThreadAuthor);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeThread && ExportConfiguration.IncludeThreadPostedAt,
            PostExporter.Resources.Localization.Resources.PlaceholderThreadPostedAt);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeThread && ExportConfiguration.IncludeThreadUrl,
            PostExporter.Resources.Localization.Resources.PlaceholderThreadUrl);

        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString, true,
            PostExporter.Resources.Localization.Resources.PlaceholderPosts);

        // Body template
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, ExportConfiguration.IncludePostNumber,
            PostExporter.Resources.Localization.Resources.PlaceholderCurrentPostNumber);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, ExportConfiguration.IncludePostNumber,
            PostExporter.Resources.Localization.Resources.PlaceholderTotalPostNumber);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, ExportConfiguration.IncludePostAuthor,
            PostExporter.Resources.Localization.Resources.PlaceholderPostAuthor);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString,
            ExportConfiguration.IncludePostPostedAt,
            PostExporter.Resources.Localization.Resources.PlaceholderPostPostedAt);

        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, true,
            PostExporter.Resources.Localization.Resources.PlaceholderPostText);

        return missingPlaceholders.Count == 0 && unusedPlaceholders.Count == 0;
    }

    private static void ValidatePlaceholder(List<string> missingPlaceholders, List<string> unusedPlaceholders,
        string text, bool shouldContainPlaceholder, string placeholder)
    {
        if (shouldContainPlaceholder)
        {
            if (!text.Contains(placeholder))
            {
                missingPlaceholders.Add(placeholder);
            }
        }
        else
        {
            if (text.Contains(placeholder))
            {
                unusedPlaceholders.Add(placeholder);
            }
        }
    }

    private void ToggleExportButtonLoading(bool isLoading)
    {
        ExportButton.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
        LoadingProgressBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingButton.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
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
        ExportConfiguration.IncludeImages = IncludeImagesCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.ReserveOrder = ReserveOrderCheckBox.IsChecked.GetValueOrDefault();

        ExportConfiguration.UseCustomTemplates = UseCustomTemplatesCheckBox.IsChecked.GetValueOrDefault();

        ExportConfiguration.DownloadToOwnFolder = DownloadToOwnFolderCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.DownloadImages = DownloadImagesCheckBox.IsChecked.GetValueOrDefault();
        ExportConfiguration.DownloadImagesToOwnFolder = DownloadImagesToOwnFolderCheckBox.IsChecked.GetValueOrDefault();
    }

    private List<FileFormat> GetSelectedFileFormats()
    {
        return (from selectableFileFormat in SelectableFileFormats
            where selectableFileFormat.IsSelected
            select selectableFileFormat.FileFormat).ToList();
    }

    private async Task PrepareAndExport(Thread thread, StringBuilder textHeadTemplate, StringBuilder textBodyTemplate,
        StringBuilder htmlHeadTemplate, StringBuilder htmlBodyTemplate, CancellationToken cancellationToken)
    {
        try
        {
            var posts = await Scraper.GetPosts(thread, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await Exporter.Export(posts, textHeadTemplate, textBodyTemplate, htmlHeadTemplate, htmlBodyTemplate,
                cancellationToken);
            LoadingProgressBar.Value++;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateCustomTemplatesButton_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentConfiguration();

        var templatesDialog = new TemplatesDialog();
        templatesDialog.ShowDialog();

        if (templatesDialog.DialogResult is true)
        {
            UseCustomTemplatesCheckBox.IsChecked = true;

            if (ExportConfiguration.TextHeadTemplate != null && ExportConfiguration.HtmlHeadTemplate != null)
            {
                var textHeadString = ExportConfiguration.TextHeadTemplate.ToString();
                var htmlHeadString = ExportConfiguration.HtmlHeadTemplate.ToString();

                var includeGroupAuthor =
                    textHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderGroupAuthor) ||
                    htmlHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderGroupAuthor);
                var includeGroupPostedAt =
                    textHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderGroupPostedAt) ||
                    htmlHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderGroupPostedAt);
                var includeGroupUrl =
                    textHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderGroupUrl) ||
                    htmlHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderGroupUrl);

                IncludeGroupAuthorCheckBox.IsChecked = includeGroupAuthor;
                IncludeGroupPostedAtCheckBox.IsChecked = includeGroupPostedAt;
                IncludeGroupUrlCheckBox.IsChecked = includeGroupUrl;
                IncludeGroupCheckBox.IsChecked = includeGroupAuthor || includeGroupPostedAt || includeGroupUrl;

                var includeThreadAuthor =
                    textHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderThreadAuthor) ||
                    htmlHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderThreadAuthor);
                var includeThreadPostedAt =
                    textHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderThreadPostedAt) ||
                    htmlHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderThreadPostedAt);
                var includeThreadUrl =
                    textHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderThreadUrl) ||
                    htmlHeadString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderThreadUrl);

                IncludeThreadAuthorCheckBox.IsChecked = includeThreadAuthor;
                IncludeThreadPostedAtCheckBox.IsChecked = includeThreadPostedAt;
                IncludeThreadUrlCheckBox.IsChecked = includeThreadUrl;
                IncludeThreadCheckBox.IsChecked = includeThreadAuthor || includeThreadPostedAt || includeThreadUrl;
            }

            if (ExportConfiguration.TextBodyTemplate != null && ExportConfiguration.HtmlBodyTemplate != null)
            {
                var textBodyString = ExportConfiguration.TextBodyTemplate.ToString();
                var htmlBodyString = ExportConfiguration.HtmlBodyTemplate.ToString();

                var includePostAuthor =
                    textBodyString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderPostAuthor) ||
                    htmlBodyString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderPostAuthor);
                var includePostPostedAt =
                    textBodyString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderPostPostedAt) ||
                    htmlBodyString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderPostPostedAt);
                var includePostNumber =
                    textBodyString.Contains(PostExporter.Resources.Localization.Resources
                        .PlaceholderCurrentPostNumber) ||
                    htmlBodyString.Contains(PostExporter.Resources.Localization.Resources
                        .PlaceholderCurrentPostNumber) ||
                    textBodyString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderTotalPostNumber) ||
                    htmlBodyString.Contains(PostExporter.Resources.Localization.Resources.PlaceholderTotalPostNumber);

                IncludePostAuthorCheckBox.IsChecked = includePostAuthor;
                IncludePostPostedAtCheckBox.IsChecked = includePostPostedAt;
                IncludePostNumberCheckBox.IsChecked = includePostNumber;
            }
        }

        SentryUtil.HandleBreadcrumb(
            "Updated custom templates",
            "ExportPage",
            level: BreadcrumbLevel.Info
        );
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
        SentryUtil.HandleBreadcrumb(
            "Opened help",
            "ExportPage",
            level: BreadcrumbLevel.Info
        );

        DialogUtil.ShowHelpAndHighlight(
            (brush => SelectedContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep1),
            (brush => GoBackContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep2),
            (brush => ExportFolderContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep3),
            (brush => ExportFormatContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep4),
            (brush => TextExportOptionsContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep5),
            (brush => AdvancedExportOptionsContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep6),
            (brush => OtherExportOptionsContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep7),
            (brush => StartExportContent.Background = brush,
                PostExporter.Resources.Localization.Resources.HelpExportStep8)
        );
    }

    private void FeedbackButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleFeedback("ExportPage");
    }

    private void DirectoryPath_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (ExportConfiguration.DirectoryPath != null)
        {
            Process.Start("explorer.exe", ExportConfiguration.DirectoryPath);
        }

        e.Handled = true;
    }

    private void LoadingButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DialogUtil.ShowQuestion(PostExporter.Resources.Localization.Resources.ExportCancelExportQuestion))
        {
            _cancellationTokenSource.Cancel();

            SentryUtil.HandleBreadcrumb(
                "Cancelled export",
                "ExportPage",
                level: BreadcrumbLevel.Warning
            );
        }
    }

    public sealed class SelectableFileFormat : INotifyPropertyChanged
    {
        private FileFormat _fileFormat;
        private string _icon;
        private bool _isSelected;
        private string _label;
        private string _name;

        public SelectableFileFormat(FileFormat fileFormat, bool isSelected)
        {
            _fileFormat = fileFormat;
            _label = fileFormat.DisplayName();
            _name = fileFormat.ToString();
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
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
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

    private enum ExportError
    {
        DirectoryAccess,
        Connection,
        WordImageDownload,
        Unrecognized,
    }
}