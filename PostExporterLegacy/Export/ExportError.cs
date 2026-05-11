using System;

namespace PostExporter.Export;

public enum ExportError
{
    NoPosts,
    DirectoryAccess,
    ImageDownload,
    WordImageExport,
    Unrecognized
}

public static class ExportErrorExtensions
{
    public static string ErrorMessage(this ExportError exportError)
    {
        return exportError switch
        {
            ExportError.NoPosts => Resources.Localization.Resources.ErrorExportFailedNoPosts,
            ExportError.DirectoryAccess => Resources.Localization.Resources.ErrorExportFailedDirectoryAccess,
            ExportError.ImageDownload => Resources.Localization.Resources.ErrorExportFailedImageDownload,
            ExportError.WordImageExport => Resources.Localization.Resources.ErrorExportFailedWordImageExport,
            ExportError.Unrecognized => Resources.Localization.Resources.ErrorExportFailedUnrecognized,
            _ => throw new ArgumentOutOfRangeException(nameof(exportError), exportError, @"Unknown value")
        };
    }
}