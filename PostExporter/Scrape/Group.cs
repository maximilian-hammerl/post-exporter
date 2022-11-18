using System;

namespace PostExporter.Scrape;

public record Group(int Id, string Author, DateTime PostedAt, string Title, string Url) : PostedText(Author, PostedAt)
{
    public sealed override string ToString()
    {
        return Title;
    }
}