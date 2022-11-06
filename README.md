# RSH Exporter

[![GitHub](https://img.shields.io/github/license/maximilian-hammerl/rsh-exporter)](https://choosealicense.com/licenses/mit/)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/maximilian-hammerl/rsh-exporter)](https://github.com/maximilian-hammerl/rsh-exporter/releases)
![GitHub Workflow Status](https://img.shields.io/github/workflow/status/maximilian-hammerl/rsh-exporter/CodeQL)

.NET 6.0 [WPF](https://learn.microsoft.com/en-us/visualstudio/designers/getting-started-with-wpf) application to export [Rollenspielhimmel](https://rollenspielhimmel.de/) groups, threads and posts to various file formats

## Dependencies

- [FontAwesome 6 Svg](https://github.com/MartinTopfstedt/FontAwesome6): WPF integration of [Font Awesome](https://fontawesome.com/)
- [Html Agility Pack](https://html-agility-pack.net): Parsing HTML
- [Open XML SDK](https://github.com/OfficeDev/Open-XML-SDK): Generating Word documents
- [Html2OpenXml](https://github.com/onizet/html2openxml): Converting HTML to OpenXml components for Word documents
- [JetBrains Code Annotation Attributes﻿](https://www.jetbrains.com/help/resharper/Code_Analysis__Code_Annotations.html)
- [Ookii Dialogs Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf): Dialogs for WPF
- [Sentry](https://sentry.io/for/csharp/): Error and performance monitoring

## Publish

Build a single executable with these settings:

```
configuration="Release"
delete_existing_files="true"
include_native_libs_for_self_extract="true"
platform="Any CPU"
produce_single_file="true"
ready_to_run="true"
runtime="win-x64"
self_contained="true"
target_folder="..."
target_framework="net6.0-windows"
```
