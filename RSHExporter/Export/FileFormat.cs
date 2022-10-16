using System;

namespace RSHExporter.Export;

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
            _ => throw new NotSupportedException(fileFormat.ToString())
        };
    }

    public static string DisplayName(this FileFormat fileFormat)
    {
        return fileFormat switch
        {
            FileFormat.Txt => Resources.Localization.Resources.FileFormatTxt,
            FileFormat.Html => Resources.Localization.Resources.FileFormatHtml,
            FileFormat.Docx => Resources.Localization.Resources.FileFormatDocx,
            _ => throw new NotSupportedException(fileFormat.ToString())
        };
    }
}