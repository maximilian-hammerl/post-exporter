<p align="center">
  <img alt="Post Exporter Logo" width="60%" src="PostExporter/Resources/splash-screen-logo.png">
</p>

[![GitHub](https://img.shields.io/github/license/maximilian-hammerl/post-exporter)](https://choosealicense.com/licenses/mit/)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/maximilian-hammerl/post-exporter)](https://github.com/maximilian-hammerl/post-exporter/releases)
[![GitHub top language](https://img.shields.io/github/languages/top/maximilian-hammerl/post-exporter)](https://github.com/maximilian-hammerl/post-exporter/search?l=c%23)
![GitHub Downloads](https://img.shields.io/github/downloads/maximilian-hammerl/post-exporter/total)

.NET 9.0 [WPF](https://learn.microsoft.com/en-us/visualstudio/designers/getting-started-with-wpf) application to export groups, threads and posts of [Yooco](https://www.yooco.de) forums to various file formats

## Screenshots

|                              Page |                                     Screenshot                                      |
|----------------------------------:|:-----------------------------------------------------------------------------------:|
|                        Login Page |  <img alt="Post Exporter Login Page" width="60%" src="screenshots/page-login.png">  |
| Groups and Threads Selecting Page | <img alt="Post Exporter Select Page" width="60%" src="screenshots/page-select.png"> |
|                    Exporting Page | <img alt="Post Exporter Export Page" width="60%" src="screenshots/page-export.png"> |

## Dependencies

- [FontAwesome 6 Svg](https://github.com/MartinTopfstedt/FontAwesome6): WPF integration of [Font Awesome](https://fontawesome.com/)
- [Html Agility Pack](https://html-agility-pack.net): Parsing HTML
- [Open XML SDK](https://github.com/OfficeDev/Open-XML-SDK): Generating Word documents
- [Html2OpenXml](https://github.com/onizet/html2openxml): Converting HTML to OpenXml components for Word documents
- [JetBrains Code Annotation Attributes](https://www.jetbrains.com/help/resharper/Code_Analysis__Code_Annotations.html)
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
target_folder="$PROJECT_DIR$/PATH/TO/TARGET_FOLDER"
target_framework="net9.0-windows"
```
