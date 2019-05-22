<!--
.net core, windowsservices
-->

# .NET Core Workers as Directory Synchronization Services
 ByteSync directory synchronization worker service is based on source code of BlinkSync command line ([http://blinksync.sourceforge.net](http://blinksync.sourceforge.net)).

## Configuration
 ByteSync reads from _Microsoft.Extensions.Configuration_, .NET Core's `appsettings.json` file. Configuration is read from the `StorageSync` section.

```json
{
  "Logging": {
	"LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "StorageSync": {
    "SyncInterval": 10,
    "DirSrc": "c:\\SourceFolder",
    "DirDest": "c:\\DestinationFolder",
    "ExcludeHidden": true,
    "DeleteFromDest": true,
    "ExcludeFiles": [],
    "ExcludeDirs": [],
    "IncludeFiles": []
    "IncludeDirs": [],
    "DeleteExcludeFiles": [],
    "DeleteExcludeDirs": []
  }
}
```
### Filter options
 There are many other filter options for more control if needed.

- **DeleteFromDest**: Delete files and directories in destination which do not appear in source
- **DeleteExcludeFiles**: Exclude files from deletion that match any of the filespecs
- **DeleteExcludeDirs**: Exclude directories from deletion that match any of the 
- **ExcludeHidden**: Exclude hidden files and directory from source
- **ExcludeFiles**: Exclude files from source that match any of the filespecs
- **ExcludeDirs**: Exclude directories from source that match any of the filespecs
- **IncludeFiles**: Only include files from source that match one of the filespecs
- **IncludeDirs**: Only include directories from source that match one of the filespecs

Note: **IncludeFiles** and **ExcludeFiles** or **IncludeDirs** and **ExcludeDirs** may not be combined in the same filter. 

Wildcards matching in paths can be used as filters. You can use '?' to match any single character and '*' to match zero or more of any characters.
```json
{
  "StorageSync": {
    "IncludeFiles": ["foo.*", "ba?.txt"],
  }
}
```

**DeleteExcludeFiles** and **DeleteExcludeDirs** exclude-from-deletion options require deletion **DeleteFromDest** enabled to be affective.
```json
{
  "StorageSync": {
    "DeleteFromDest": true,
    "DeleteExcludeFiles": ["foo.txt", "bar.txt", "foo\foo1.txt", "foo\foo2.txt"],
    "DeleteExcludeDirs": ["bin", "obj"]
  }
}
```

**IncludeFiles** and **ExcludeDirs** files options may be combined.
```json
{
  "StorageSync": {
    "ExcludeFiles": ["foo.txt", "bar.txt", "foo\foo1.txt", "foo\foo2.txt"],
    "ExcludeDirs": ["bin", "obj"]
  }
}
```

**ExcludeFiles** and **ExcludeDirs** directories options may be combined.
```json
{
  "StorageSync": {
    "ExcludeFiles": ["foo.txt", "bar.txt", "foo\foo1.txt", "foo\foo2.txt"],
    "ExcludeDirs": ["bin", "obj"],
  }
}
```

### Level overrides
The default `Logging` configuration property can be set to `Trace` displaying all file commands. This is useful for validating include and exclude filters.
```json
"Logging": {
	"LogLevel": {
		"Default": "Trace",
		"Microsoft": "Warning",
		"Microsoft.Hosting.Lifetime": "Information"
	}
}
```

### Environment variables
You can add or override ByteSync configuration through the environment.  For example, to set the sync interval, directory source and destination using the _Windows_ command prompt:

```console
set STORAGESYNC__SYNCINTERVAL=30
set STORAGESYNC__DIRSRC=c:\SourceFolder
set STORAGESYNC__DIRDEST=c:\DestinationFolder
```
### Command line
You can add or override ByteSync configuration through the command.  For example, to set the sync interval, directory source and destination using the _Windows_ command prompt:
```console
byteSync.exe --StorageSync:SyncInterval=30 --StorageSync:DirSrc="c:\SourceFolder" --StorageSync:DirDest="c:\DestinationFolder"
```

## Run as a Windows Service
Register ByteSync as a Windows Service using the _Windows_ administrator command prompt:

```console
sc create ByteSync displayname="Bytewize ByteSync" description="Directory synchronization worker service" binpath="<path>"
```
Running ByteSync as a windows service will output messages to the windows event log.

## Build your own version

If you would like to modify the source code. Run the following _Windows_ command according to your target platform.

### Windows
```console
dotnet publish --configuration Release --runtime win-x64 --output "<path>"
```

### Linux (portable)
```console
dotnet publish --configuration Release --runtime linux-x64 --output "<path>"
```

### MacOS (OS X)
```console
dotnet publish --configuration Release --runtime osx-x64 --output "<path>"
```
