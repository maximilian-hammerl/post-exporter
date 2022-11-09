using System;

namespace RSHExporter.Scrape;

public record Group(int Id, string Author, DateTime PostedAt, string Title, string Url) : PostedText(Author, PostedAt)
{
    public sealed override string ToString()
    {
        return Title;
    }
}