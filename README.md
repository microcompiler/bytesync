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
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information"
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
    "IncludeFiles": [],
    "IncludeDirs": [],
    "DeleteExcludeFiles": [],
    "DeleteExcludeDirs": []
  }
}
```

 There are many other options for more control if you need it.

- **DeleteFromDest**: Delete files and directories in destination which do not appear in source
- **DeleteExcludeFiles**: Exclude files from deletion that match any of the filespecs
- **DeleteExcludeDirs**: Exclude directories from deletion that match any of the 
- **ExcludeHidden**: Exclude hidden files and directory from source
- **ExcludeFiles**: Exclude files from source that match any of the filespecs
- **ExcludeDirs**: Exclude directories from source that match any of the filespecs
- **IncludeFiles**: Only include files from source that match one of the filespecs
- **IncludeDirs**: Only include directories from source that match one of the filespecs
filespecs

Note: **IncludeFiles** and **ExcludeFiles** or **IncludeDirs** and **ExcludeDirs** may not be combined in the same filter. 

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

**IncludeFiles** and **ExcludeFiles** files options may be combined.
```json
{
  "StorageSync": {
    "ExcludeFiles": ["foo.txt", "bar.txt", "foo\foo1.txt", "foo\foo2.txt"],
    "ExcludeDirs": ["bin", "obj"]
  }
}
```

**IncludeDirs** and **ExcludeDirs** directories options may be combined.
```json
{
  "StorageSync": {
    "ExcludeFiles": ["foo.txt", "bar.txt", "foo\foo1.txt", "foo\foo2.txt"],
    "ExcludeDirs": ["bin", "obj"],
  }
}
```


### Level overrides

The `Logging` configuration property can be set to a single value as in the sample above, or, levels can be overridden per logging source.

This is useful in ASP.NET Core applications, which will often specify minimum level as:

```json
"Logging": {
  "LogLevel": {
    "Default": "Debug",
    "System": "Information",
    "Microsoft": "Information"
  }
}
```

### Environment variables

You can add or override ByteSync configuration through the environment.  For example, to set the sync interval using the _Windows_ command prompt:

```
set StorageSync:SyncInterval=90
```

## Run as a Windows Service
Register ByteSync as a Windows Service using the _Windows_ administrator command prompt:

```
sc create ByteSync displayname="Bytewize ByteSync" binpath="<path>"
```
## Build your own version

If you would like to modify the source code. Run the following _Windows_ command according to your target platform.

### Windows
```
dotnet publish --configuration Release --runtime win-x64 --output "<path>"
```

### Linux (portable)
```
dotnet publish --configuration Release --runtime linux-x64 --output "<path>"
```

### MacOS (OS X)
```
dotnet publish --configuration Release --runtime osx-x64 --output "<path>"
```
