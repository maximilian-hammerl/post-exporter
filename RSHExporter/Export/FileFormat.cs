using System;

namespace RSHExporter.Export;

public enum FileFormat
{
    Text,
    Html,
    Word
}

public static class FileFormatExtensions
{
    public static string FileExtension(this FileFormat fileFormat)
    {
        return fileFormat switch
        {
            FileFormat.Text => "txt",
            FileFormat.Html => "html",
            FileFormat.Word => "docx",
            _ => throw new NotSupportedException(fileFormat.ToString())
        };
    }

    public static string DisplayName(this FileFormat fileFormat)
    {
        return fileFormat switch
        {
            FileFormat.Text => "Text File (.txt)",
            FileFormat.Html => "HTML File (.html)",
            FileFormat.Word => "Word File (.docx)",
            _ => throw new NotSupportedException(fileFormat.ToString())
        };
    }
}