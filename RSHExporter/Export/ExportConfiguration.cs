using System;
using System.Collections.Generic;

namespace RSHExporter.Export;

public static class ExportConfiguration
{
    public static string? DirectoryPath { get; set; } = null;

    public static string ExportDirectoryPath
    {
        get
        {
            if (DirectoryPath == null)
            {
                throw new ArgumentException("Directory path is missing");
            }

            return DirectoryPath;
        }
    }

    public static List<FileFormat> FileFormats { get; set; } = new(3);
    public static bool IncludeGroup { get; set; } = false;
    public static bool IncludeGroupAuthor { get; set; } = false;
    public static bool IncludeGroupPostedAt { get; set; } = false;
    public static bool IncludeGroupUrl { get; set; } = false;
    public static bool IncludeThread { get; set; } = false;
    public static bool IncludeThreadAuthor { get; set; } = false;
    public static bool IncludeThreadPostedAt { get; set; } = false;
    public static bool IncludeThreadUrl { get; set; } = false;
    public static bool IncludePostAuthor { get; set; } = false;
    public static bool IncludePostPostedAt { get; set; } = false;
    public static bool IncludePostNumber { get; set; } = false;
    public static bool IncludePageNumber { get; set; } = false;
    public static bool DownloadToOwnFolder { get; set; } = false;
    public static bool IncludeImages { get; set; } = true;
    public static bool DownloadImages { get; set; } = false;
    public static bool DownloadImagesToOwnFolder { get; set; } = false;
    public static bool ReserveOrder { get; set; } = false;
}