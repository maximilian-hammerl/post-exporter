using System;

namespace PostExporter.Scrape;

public record Thread(int Id, string Author, DateTime PostedAt, string Title, string Url, Group Group) : PostedText(
    Author, PostedAt)
{
    public sealed override string ToString()
    {
        return Title;
    }
}