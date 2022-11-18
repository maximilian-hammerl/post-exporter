using System;
using System.Collections.Generic;
using HtmlAgilityPack;

namespace PostExporter.Scrape;

public record Post(string Author, DateTime PostedAt, List<HtmlNode> TextNodes, Thread Thread) : PostedText(Author,
    PostedAt);