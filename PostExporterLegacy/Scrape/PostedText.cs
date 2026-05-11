using System;

namespace PostExporter.Scrape;

public abstract record PostedText(string Author, DateTime PostedAt);