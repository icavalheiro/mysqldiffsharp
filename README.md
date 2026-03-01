# MySQL-Diff Sharp

MySQL-Diff Sharp is a rewrite in C# of the original CLI script made in Perl for comparing the schemas of two MySQL/MariaDB databases and generating diff sql.

## Why

Why recreate the original Perl CLI in C#? Becouse the original needs Perl to run, which is painfull. This rewrite is a standalone executable that is easy to import into any CI/CD pipeline or docker containers.
It also makes use of parallel CPU threads to speed up things, while the original CLI tool uses `mysqldump`.
This project has no external dynamic dependecies such as the original Perl implementation.

Summing up reasons
 - no dependency on `mysql` nor `mysqldump` cli tools
 - parallel table dumping based on available CPU cores for threads
 - precompiled REGEX (yeah, C# allows us to compile REGEX for REALLY fast matches)
 - compiled to native speed, blazing fast startup times and low memory usage

## Prerequisites

Just grab the binary for your own os from the release pages. This tools is self-container

## Building

To build the app just make sure you have `dotnet >= 10` and then:

```bash
dotnet publish -c Release
```


