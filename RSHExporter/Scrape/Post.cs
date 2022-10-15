using System;
using HtmlAgilityPack;

namespace RSHExporter.Scrape;

public record Post(string Author, DateTime PostedAt, HtmlNode Node, Thread Thread) : PostedText(Author, PostedAt);