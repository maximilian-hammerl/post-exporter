using System;

namespace PostExporter.Export;

public enum FileFormat
{
    Txt,
    Html,
    Docx
}

public static class FileFormatExtensions
{
    public static string FileExtension(this FileFormat fileFormat)
    {
        return fileFormat switch
        {
            FileFormat.Txt => "txt",
            FileFormat.Html => "html",
            FileFormat.Docx => "docx",
            _ => throw new ArgumentOutOfRangeException(nameof(fileFormat), fileFormat, @"Unknown value")
        };
    }

    public static string DisplayName(this FileFormat fileFormat)
    {
        return fileFormat switch
        {
            FileFormat.Txt => Resources.Localization.Resources.FileFormatTxt,
            FileFormat.Html => Resources.Localization.Resources.FileFormatHtml,
            FileFormat.Docx => Resources.Localization.Resources.FileFormatDocx,
            _ => throw new ArgumentOutOfRangeException(nameof(fileFormat), fileFormat, @"Unknown value")
        };
    }

    public static string Icon(this FileFormat fileFormat)
    {
        return fileFormat switch
        {
            FileFormat.Txt => "Solid_FileLines",
            FileFormat.Html => "Solid_FileCode",
            FileFormat.Docx => "Solid_FileWord",
            _ => throw new ArgumentOutOfRangeException(nameof(fileFormat), fileFormat, @"Unknown value")
        };
    }
}