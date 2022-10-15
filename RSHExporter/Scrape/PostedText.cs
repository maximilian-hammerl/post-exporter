using System;

namespace RSHExporter.Scrape;

public abstract record PostedText(string Author, DateTime PostedAt);