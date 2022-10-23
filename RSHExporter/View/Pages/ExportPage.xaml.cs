using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using RSHExporter.Export;
using RSHExporter.Scrape;
using RSHExporter.Utils;
using Sentry;
using Thread = RSHExporter.Scrape.Thread;

namespace RSHExporter.View.Pages;

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
            message: "Opened page",
            category: "ExportPage",
            level: BreadcrumbLevel.Info
        );
    }

    [UsedImplicitly] public ObservableCollection<Thread> SelectedThreads { get; set; }
    [UsedImplicitly] public ObservableCollection<SelectableFileFormat> SelectableFileFormats { get; set; }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        SentryUtil.HandleBreadcrumb(
            message: "Back to selecting groups and threads",
            category: "ExportPage",
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

            if (Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length > 0)
            {
                if (!DialogUtil.ShowQuestion(RSHExporter.Resources.Localization.Resources
                        .ExportFolderContainsFilesQuestion))
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

        DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.WarningSelectedDocx);
    }

    private async void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleExportButtonLoading(true);

        SentryUtil.HandleBreadcrumb(
            message: "Started export",
            category: "ExportPage",
            level: BreadcrumbLevel.Info
        );

        SaveCurrentConfiguration();

        LoadingProgressBar.Value = 0;
        LoadingProgressBar.Maximum = _threads.Count;

        if (string.IsNullOrWhiteSpace(ExportConfiguration.DirectoryPath))
        {
            SentryUtil.HandleBreadcrumb(
                message: "Missing folder",
                category: "ExportPage",
                level: BreadcrumbLevel.Warning
            );

            ToggleExportButtonLoading(false);

            ExportFolderContent.Background = Brushes.LightBlue;
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.WarningMissingFolder);
            ExportFolderContent.Background = Brushes.White;
            return;
        }

        if (ExportConfiguration.FileFormats.Count == 0)
        {
            SentryUtil.HandleBreadcrumb(
                message: "Missing file format",
                category: "ExportPage",
                level: BreadcrumbLevel.Warning
            );

            ToggleExportButtonLoading(false);

            ExportFormatContent.Background = Brushes.LightBlue;
            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.WarningMissingFileFormat);
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
                    message: "Html template not complete",
                    category: "ExportPage",
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
                    message: "Text template not complete",
                    category: "ExportPage",
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

        var failedExports = new ConcurrentBag<Thread>();

        _cancellationTokenSource = new CancellationTokenSource();

        await Task.WhenAll(_threads.Select(async thread =>
        {
            try
            {
                await PrepareAndExport(thread, textHeadTemplate, textBodyTemplate, htmlHeadTemplate, htmlBodyTemplate,
                    _cancellationTokenSource.Token);
            }
            catch (Exception exception)
            {
                failedExports.Add(thread);

                SentryUtil.HandleException(exception);
            }
        }));

        ToggleExportButtonLoading(false);

        if (!failedExports.IsEmpty)
        {
            SentryUtil.HandleBreadcrumb(
                message: $"{failedExports.Count} of {_threads.Count} exports failed",
                category: "ExportPage",
                level: BreadcrumbLevel.Error
            );

            DialogUtil.ShowError(
                string.Format(RSHExporter.Resources.Localization.Resources.ErrorExportFailed,
                    string.Join(", ", failedExports.Select(thread => $"{thread.Title} ({thread.Group.Title})"))), true);
        }
        else if (_cancellationTokenSource.IsCancellationRequested)
        {
            SentryUtil.HandleBreadcrumb(
                message: $"Export of {_threads.Count} threads was cancelled",
                category: "ExportPage",
                level: BreadcrumbLevel.Warning
            );

            DialogUtil.ShowWarning(RSHExporter.Resources.Localization.Resources.WarningExportCancelled);
        }
        else
        {
            var numberFilesExported = _threads.Count * ExportConfiguration.FileFormats.Count;

            SentryUtil.HandleBreadcrumb(
                message: $"Successfully exported {numberFilesExported} files",
                category: "ExportPage",
                level: BreadcrumbLevel.Info
            );

            DialogUtil.ShowInformation(numberFilesExported == 1
                ? RSHExporter.Resources.Localization.Resources.InfoFileSuccessfullyExported
                : string.Format(RSHExporter.Resources.Localization.Resources.InfoFilesSuccessfullyExported,
                    numberFilesExported)
            );
        }

        Process.Start("explorer.exe", ExportConfiguration.DirectoryPath);
    }

    private static void ShowErrorMessageForTemplate(string templateType, List<string> missingPlaceholders,
        List<string> unusedPlaceholder)
    {
        var errorMessage = string.Format(RSHExporter.Resources.Localization.Resources.ErrorTemplates, templateType);

        var placeHolderErrorMessage = new List<string>(2);

        switch (missingPlaceholders.Count)
        {
            case 1:
                placeHolderErrorMessage.Add(string.Format(
                    RSHExporter.Resources.Localization.Resources.ErrorMissingPlaceholder, missingPlaceholders[0])
                );
                break;
            case > 1:
                placeHolderErrorMessage.Add(string.Format(
                    RSHExporter.Resources.Localization.Resources.ErrorMissingPlaceholders,
                    string.Join(", ", missingPlaceholders))
                );
                break;
        }

        switch (unusedPlaceholder.Count)
        {
            case 1:
                placeHolderErrorMessage.Add(string.Format(
                    RSHExporter.Resources.Localization.Resources.ErrorUnusedPlaceholder, unusedPlaceholder[0])
                );
                break;
            case > 1:
                placeHolderErrorMessage.Add(string.Format(
                    RSHExporter.Resources.Localization.Resources.ErrorUnusedPlaceholders,
                    string.Join(", ", unusedPlaceholder))
                );
                break;
        }

        errorMessage +=
            $" {Util.CapitalizeFirstChar(string.Join($" {RSHExporter.Resources.Localization.Resources.And} ", placeHolderErrorMessage))}.";

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
            RSHExporter.Resources.Localization.Resources.PlaceholderGroupTitle);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeGroup && ExportConfiguration.IncludeGroupAuthor,
            RSHExporter.Resources.Localization.Resources.PlaceholderGroupAuthor);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeGroup && ExportConfiguration.IncludeGroupPostedAt,
            RSHExporter.Resources.Localization.Resources.PlaceholderGroupPostedAt);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeGroup && ExportConfiguration.IncludeGroupUrl,
            RSHExporter.Resources.Localization.Resources.PlaceholderGroupUrl);

        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString, ExportConfiguration.IncludeThread,
            RSHExporter.Resources.Localization.Resources.PlaceholderThreadTitle);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeThread && ExportConfiguration.IncludeThreadAuthor,
            RSHExporter.Resources.Localization.Resources.PlaceholderThreadAuthor);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeThread && ExportConfiguration.IncludeThreadPostedAt,
            RSHExporter.Resources.Localization.Resources.PlaceholderThreadPostedAt);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString,
            ExportConfiguration.IncludeThread && ExportConfiguration.IncludeThreadUrl,
            RSHExporter.Resources.Localization.Resources.PlaceholderThreadUrl);

        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, headString, true,
            RSHExporter.Resources.Localization.Resources.PlaceholderPosts);

        // Body template
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, ExportConfiguration.IncludePostNumber,
            RSHExporter.Resources.Localization.Resources.PlaceholderCurrentPostNumber);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, ExportConfiguration.IncludePostNumber,
            RSHExporter.Resources.Localization.Resources.PlaceholderTotalPostNumber);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, ExportConfiguration.IncludePostAuthor,
            RSHExporter.Resources.Localization.Resources.PlaceholderPostAuthor);
        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString,
            ExportConfiguration.IncludePostPostedAt,
            RSHExporter.Resources.Localization.Resources.PlaceholderPostPostedAt);

        ValidatePlaceholder(missingPlaceholders, unusedPlaceholders, bodyString, true,
            RSHExporter.Resources.Localization.Resources.PlaceholderPostText);

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
                    textHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderGroupAuthor) ||
                    htmlHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderGroupAuthor);
                var includeGroupPostedAt =
                    textHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderGroupPostedAt) ||
                    htmlHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderGroupPostedAt);
                var includeGroupUrl =
                    textHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderGroupUrl) ||
                    htmlHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderGroupUrl);

                IncludeGroupAuthorCheckBox.IsChecked = includeGroupAuthor;
                IncludeGroupPostedAtCheckBox.IsChecked = includeGroupPostedAt;
                IncludeGroupUrlCheckBox.IsChecked = includeGroupUrl;
                IncludeGroupCheckBox.IsChecked = includeGroupAuthor || includeGroupPostedAt || includeGroupUrl;

                var includeThreadAuthor =
                    textHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderThreadAuthor) ||
                    htmlHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderThreadAuthor);
                var includeThreadPostedAt =
                    textHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderThreadPostedAt) ||
                    htmlHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderThreadPostedAt);
                var includeThreadUrl =
                    textHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderThreadUrl) ||
                    htmlHeadString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderThreadUrl);

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
                    textBodyString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderPostAuthor) ||
                    htmlBodyString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderPostAuthor);
                var includePostPostedAt =
                    textBodyString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderPostPostedAt) ||
                    htmlBodyString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderPostPostedAt);
                var includePostNumber =
                    textBodyString.Contains(RSHExporter.Resources.Localization.Resources
                        .PlaceholderCurrentPostNumber) ||
                    htmlBodyString.Contains(RSHExporter.Resources.Localization.Resources
                        .PlaceholderCurrentPostNumber) ||
                    textBodyString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderTotalPostNumber) ||
                    htmlBodyString.Contains(RSHExporter.Resources.Localization.Resources.PlaceholderTotalPostNumber);

                IncludePostAuthorCheckBox.IsChecked = includePostAuthor;
                IncludePostPostedAtCheckBox.IsChecked = includePostPostedAt;
                IncludePostNumberCheckBox.IsChecked = includePostNumber;
            }
        }

        SentryUtil.HandleBreadcrumb(
            message: "Updated custom templates",
            category: "ExportPage",
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
            message: "Opened help",
            category: "ExportPage",
            level: BreadcrumbLevel.Info
        );

        DialogUtil.ShowHelpAndHighlight(
            (brush => SelectedContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep1),
            (brush => GoBackContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep2),
            (brush => ExportFolderContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep3),
            (brush => ExportFormatContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep4),
            (brush => TextExportOptionsContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep5),
            (brush => AdvancedExportOptionsContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep6),
            (brush => OtherExportOptionsContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep7),
            (brush => StartExportContent.Background = brush,
                RSHExporter.Resources.Localization.Resources.HelpExportStep8)
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
        if (DialogUtil.ShowQuestion(RSHExporter.Resources.Localization.Resources.ExportCancelExportQuestion))
        {
            _cancellationTokenSource.Cancel();

            SentryUtil.HandleBreadcrumb(
                message: "Cancelled export",
                category: "ExportPage",
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
}