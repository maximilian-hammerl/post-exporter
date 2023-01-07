using System;
using System.Windows;
using System.Windows.Navigation;
using PostExporter.Utils;

namespace PostExporter.View.Dialogs;

public partial class NewReleaseDialog : Window
{
    private readonly GitHubUtil.Release _latestRelease;

    public NewReleaseDialog(GitHubUtil.Release latestRelease, Version currentVersion)
    {
        InitializeComponent();

        _latestRelease = latestRelease;

        CurrentVersionTextBox.Text = currentVersion.ToString(2);
        LatestVersionTextBox.Text = latestRelease.GetVersion().ToString(2);

        NewReleaseDownloadUrl.Text = latestRelease.GetDownloadUrl();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void IgnoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DownloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        Util.OpenBrowser(_latestRelease.GetDownloadUrl());
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Util.OpenBrowser(e.Uri);
        e.Handled = true;
    }
}