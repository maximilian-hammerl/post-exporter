using System;
using System.Collections.Generic;
using HtmlAgilityPack;

namespace RSHExporter.Scrape;

public record Post(string Author, DateTime PostedAt, List<HtmlNode> TextNodes, Thread Thread) : PostedText(Author,
    PostedAt);