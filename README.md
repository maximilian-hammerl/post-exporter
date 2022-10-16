# RSH Exporter

[WPF](https://learn.microsoft.com/en-us/visualstudio/designers/getting-started-with-wpf) application to export [Rollenspielhimmel](https://rollenspielhimmel.de/) groups, threads and posts to various file formats

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
