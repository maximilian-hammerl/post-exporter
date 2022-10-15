using System;

namespace RSHExporter.Scrape;

public record Thread(string Author, DateTime PostedAt, string Title, string Url, Group Group) : PostedText(Author,
    PostedAt)
{
    public sealed override string ToString()
    {
        return Title;
    }
}