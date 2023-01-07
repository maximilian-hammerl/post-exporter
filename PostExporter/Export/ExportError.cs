using System;

namespace PostExporter.Export;

public enum ExportError
{
    DirectoryAccess,
    Connection,
    WordImageDownload,
    Unrecognized,
}

public static class ExportErrorExtensions
{
    public static string ErrorMessage(this ExportError exportError)
    {
        return exportError switch
        {
            ExportError.DirectoryAccess => Resources.Localization.Resources.ErrorExportFailedDirectoryAccess,
            ExportError.Connection => Resources.Localization.Resources.ErrorExportFailedConnection,
            ExportError.WordImageDownload => Resources.Localization.Resources.ErrorExportFailedWordImageDownload,
            ExportError.Unrecognized => Resources.Localization.Resources.ErrorExportFailedUnrecognized,
            _ => throw new ArgumentOutOfRangeException(nameof(exportError), exportError, @"Unknown value")
        };
    }
}