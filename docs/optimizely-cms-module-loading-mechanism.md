# Optimizely CMS Module Loading Mechanism

## Table of Contents

- [Overview](#overview)
- [Module Types: Protected vs Public](#module-types-protected-vs-public)
- [Module Discovery Pipeline](#module-discovery-pipeline)
- [module.config Reference](#moduleconfig-reference)
- [Client-Side Path Resolution (Dojo AMD)](#client-side-path-resolution-dojo-amd)
- [Registering a Protected Module](#registering-a-protected-module)
- [Packaging a Module as a NuGet Package](#packaging-a-module-as-a-nuget-package)
- [Common Pitfalls](#common-pitfalls)

---

## Overview

Optimizely CMS uses a modular architecture where self-contained **modules** provide both server-side (controllers, services) and client-side (Dojo AMD scripts, CSS) functionality for the editor UI.

The module system is built on these core components:

| Component                                                       | Role                                                                                                                      |
| --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `ProtectedModuleOptions` / `PublicModuleOptions`                | Configuration: lists which modules to load and where to find them                                                         |
| `ConfigModuleProvider`                                          | Orchestrator: reads the options and delegates to `ModuleFinder`                                                           |
| `ModuleFinder`                                                  | Discovery: locates module directories, reads `module.config`, loads assemblies                                            |
| `ShellZipArchiveVirtualPathProviderModule` / `ZipArchiveFinder` | Zip handling: discovers `{name}.zip` archives inside module folders and serves their contents via a virtual file provider |
| `ModuleTable`                                                   | Runtime registry: resolves module names to client resource paths                                                          |
| `DojoConfigurationHelper`                                       | Dojo integration: builds the Dojo AMD `paths` configuration from all registered modules                                   |

---

## Module Types: Protected vs Public

### Protected Modules

- Located physically at `~/modules/_protected/{ModuleName}/`
- Served at the virtual path `~/EPiServer/{ModuleName}/` (mapped by the CMS framework)
- Require authentication - the default authorization policy `CmsPolicyNames.DefaultShellModule` is applied automatically if none is specified
- **Not auto-discovered** - each module must be explicitly registered in `ProtectedModuleOptions.Items`
- Default root path: `~/EPiServer/`
- Default auto-discovery level: `Minimal` (no subdirectory scanning)
- All built-in CMS modules (Shell, CMS, etc.) are protected modules

The built-in protected modules registered by default:

```text
Shell, CMS, EPiServer.Cms.TinyMce, EPiServer.Labs.LinkItemProperty,
Settings, Profile
```

### Public Modules

- Located at `~/modules/{ModuleName}/`
- Served directly from the same path
- No authentication required
- **Auto-discovered** - `PublicModuleOptions.AutoDiscovery` defaults to `AutoDiscoveryLevel.Modules`, meaning all subdirectories under `~/modules/` are scanned
- Default root path: `~/modules/`

### AutoDiscoveryLevel

```csharp
public enum AutoDiscoveryLevel
{
    /// Only load modules explicitly configured in options.
    /// Only associate assemblies explicitly listed.
    Minimal = 1,

    /// Auto-discover modules in the module directory.
    /// Load assemblies from module.config and from the module's bin directory.
    Modules = 2
}
```

Since `ProtectedModuleOptions.AutoDiscovery` defaults to `Minimal`, **protected modules are never auto-discovered**. This is the most common stumbling block for library developers.

---

## Module Discovery Pipeline

### 1. Configuration Phase (Startup)

During application startup, `ConfigModuleProvider.GetModules()` is called. It processes both `ProtectedModuleOptions` and `PublicModuleOptions`:

```text
ProtectedModuleOptions
  ├── RootPath = "~/EPiServer/"
  ├── AutoDiscovery = Minimal
  └── Items = [ { Name: "Shell" }, { Name: "CMS" }, ... ]

PublicModuleOptions
  ├── RootPath = "~/modules/"
  ├── AutoDiscovery = Modules
  └── Items = [ ... ]
```

### 2. Zip Archive Discovery

Before modules are resolved, `ShellZipArchiveVirtualPathProviderModule` discovers zip archives:

1. **Public modules**: Scans `~/modules/` for zip archives
2. **Protected modules**: Scans `~/modules/_protected/` for zip archives, but serves them at the virtual path `~/EPiServer/`

The `ZipArchiveFinder` looks for zip files using this convention:

```text
modules/_protected/{ModuleName}/{ModuleName}.zip
```

For example:

```text
modules/_protected/MyCompany.MyModule/MyCompany.MyModule.zip
```

Each discovered zip gets a `ZipArchiveFileProvider` registered in the composite file provider, making the zip contents accessible at the virtual path `~/EPiServer/{ModuleName}/`.

### 3. Module Resolution (ModuleFinder)

For each module in `ProtectedModuleOptions.Items`, `ConfigModuleProvider` calls `ModuleFinder.GetModuleInDirectory()`:

1. **Compute the resource path** from the template `{rootpath}{modulename}` → e.g. `~/EPiServer/MyCompany.MyModule/`
2. **Look for `module.config`** at `{resourcePath}/module.config`
3. **Parse `module.config`** into a `ShellModuleManifest` via XML deserialization
4. **Determine `ClientResourcePath`**:
   - If `module.config` has `clientResourceRelativePath` attribute → `ClientResourcePath = ResourceBasePath + clientResourceRelativePath + "/"`
   - Otherwise → `ClientResourcePath = ResourceBasePath` (same as the module root)
5. **Load assemblies** declared in `<assemblies>` from the application's loaded assemblies or from the module's bin directory

### 4. Dojo Path Registration

`DojoConfigurationHelper.RegisterModulePaths()` iterates all registered modules and builds the Dojo AMD configuration:

For each `<dojo><paths><add name="..." path="..." /></paths></dojo>` entry in `module.config`:

- If the path is absolute → use it directly
- If the path is relative → resolve it via `Paths.ToClientResource(moduleName, path)`

`Paths.ToClientResource()` calls `ModuleTable.ResolveClientPath()`:

```text
ModuleTable.ResolveClientPath(moduleName, relativePath)
  → Combine(module.ClientResourcePath, relativePath)
  → e.g. "~/EPiServer/MyCompany.MyModule/ClientResources/" + "scripts"
  → "/EPiServer/MyCompany.MyModule/ClientResources/scripts"
```

This is the URL the browser actually requests for Dojo modules.

---

## module.config Reference

The `module.config` file is the module manifest. It lives at the root of the module directory (or zip archive) and is deserialized into `ShellModuleManifest`.

### Complete Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<module clientResourceRelativePath="ClientResources">
  <assemblies>
    <add assembly="MyCompany.MyModule" />
  </assemblies>
  <clientModule initializer="my-company/my-module/initializer">
    <moduleDependencies>
      <add dependency="CMS" type="RunAfter" />
    </moduleDependencies>
  </clientModule>
  <dojo>
    <paths>
      <add name="my-company/my-module" path="scripts" />
    </paths>
  </dojo>
  <clientResources>
    <add name="epi-cms.widgets.base"
         path="styles/content-area-options.css"
         resourceType="Style" />
  </clientResources>
</module>
```

### Element Reference

#### `<module>` (root element)

| Attribute                    | Required | Description                                                                                                                                                                                                |
| ---------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `clientResourceRelativePath` | **Yes*** | Relative path from the module root to client resources. Typically `"ClientResources"`. Without this, client resources are served from the module root, causing path mismatches for resources inside a zip. |
| `loadFromBin`                | No       | Whether to load assemblies from the module's bin directory. Default: `true`.                                                                                                                               |
| `routeBasePath`              | No       | Base path for MVC routes.                                                                                                                                                                                  |
| `authorizationPolicy`        | No       | Server-side authorization policy. Defaults to `DefaultShellModule` for protected modules.                                                                                                                  |
| `clientAuthorizationPolicy`  | No       | Client-side authorization policy. Defaults to `DefaultShellModule` for protected modules.                                                                                                                  |
| `viewFolder`                 | No       | Custom view folder path.                                                                                                                                                                                   |
| `version`                    | No       | Module version.                                                                                                                                                                                            |

*While technically optional, omitting `clientResourceRelativePath` when your zip contains resources under a `ClientResources/` subfolder will cause all client resource paths to resolve incorrectly.

#### `<assemblies>`

Declares .NET assemblies belonging to this module:

```xml
<assemblies>
  <add assembly="MyCompany.MyModule" />
</assemblies>
```

The CMS first tries to find the assembly among already-loaded assemblies (by name). If not found and `loadFromBin` is true, it reads the assembly bytes from the module's bin directory.

#### `<clientModule>`

Declares the client-side (Dojo AMD) initializer:

```xml
<clientModule initializer="my-company/my-module/initializer">
  <moduleDependencies>
    <add dependency="CMS" type="RunAfter" />
  </moduleDependencies>
</clientModule>
```

- `initializer`: The Dojo module ID that will be loaded when the editor UI starts. This must match a path registered in `<dojo><paths>`.
- `<moduleDependencies>`: Declares ordering constraints relative to other modules. `RunAfter` ensures your initializer runs after the dependency's initializer.

#### `<dojo>`

Configures Dojo AMD path mappings:

```xml
<dojo>
  <paths>
    <add name="my-company/my-module" path="scripts" />
  </paths>
</dojo>
```

- `name`: The Dojo module namespace (used in `define(["my-company/my-module/..."], ...)`)
- `path`: Relative path from `ClientResourcePath` to the script directory

The CMS resolves this to:

```text
/EPiServer/{ModuleName}/{clientResourceRelativePath}/{path}/
```

For the example above with `clientResourceRelativePath="ClientResources"`:

```text
/EPiServer/MyCompany.MyModule/ClientResources/scripts/
```

So `define(["my-company/my-module/initializer"], ...)` resolves to:

```text
/EPiServer/MyCompany.MyModule/ClientResources/scripts/initializer.js
```

#### `<clientResources>`

Declares CSS/JS resources to inject into the editor UI:

```xml
<clientResources>
  <add name="epi-cms.widgets.base"
       path="styles/my-styles.css"
       resourceType="Style" />
</clientResources>
```

- `name`: The resource group to inject into (e.g. `epi-cms.widgets.base` for editor widgets)
- `path`: Relative path from `ClientResourcePath`
- `resourceType`: `Style` or `Script`

---

## Client-Side Path Resolution (Dojo AMD)

Understanding how paths resolve is critical to debugging "404 Not Found" errors:

```text
Dojo module request: "my-company/my-module/initializer"
                          │
                          ▼
Dojo config paths: { "my-company/my-module":
    "/EPiServer/MyCompany.MyModule/ClientResources/scripts" }
                          │
                          ▼
HTTP request: GET /EPiServer/MyCompany.MyModule/ClientResources/scripts/initializer.js
                          │
                          ▼
Virtual file provider: ZipArchiveFileProvider for MyCompany.MyModule.zip
    root = ~/EPiServer/MyCompany.MyModule/
    resolves to: ClientResources/scripts/initializer.js (inside the zip)
```

Key points:

- The Dojo namespace (e.g. `my-company/my-module`) is entirely your choice - it doesn't need to match the module name
- The Dojo path (e.g. `scripts`) is relative to `ClientResourcePath`, **not** the module root
- `ClientResourcePath` = `ResourceBasePath` + `clientResourceRelativePath` + `/`
- For protected modules, `ResourceBasePath` = `~/EPiServer/{ModuleName}/`

---

## Registering a Protected Module

Since protected modules are not auto-discovered, you must explicitly register your module in `ProtectedModuleOptions`. This is typically done in your library's DI extension method:

```csharp
using EPiServer.Shell.Modules;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyModule(this IServiceCollection services)
    {
        services.Configure<ProtectedModuleOptions>(o =>
        {
            const string moduleName = "MyCompany.MyModule";
            if (!o.Items.Any(i => i.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase)))
            {
                o.Items.Add(new ModuleDetails { Name = moduleName });
            }
        });

        // Register your services...

        return services;
    }
}
```

The `ModuleDetails` class:

```csharp
public class ModuleDetails
{
    public string Name { get; set; }
    public string ResourcePath { get; set; } = "{rootpath}{modulename}";
    public IList<string> Assemblies { get; } = new List<string>();
    public string ClientResourcePath { get; set; }
}
```

- `Name` (**required**): Must exactly match the module directory/zip name
- `ResourcePath`: Template for locating the module. Default `"{rootpath}{modulename}"` is usually sufficient
- `Assemblies`: Additional assemblies to associate. Usually left empty if `module.config` declares them
- `ClientResourcePath`: Override the client resource path. Usually left unset (let `module.config` handle it)

> **Note**: `ProtectedModuleOptions` and `ModuleDetails` are in the `EPiServer.Shell.Modules` namespace, which is in `EPiServer.Shell.dll`. This DLL ships as part of the `EPiServer.CMS.UI.Core` NuGet package - **not** in `EPiServer.CMS.Core` or `EPiServer.Framework`.
>
> **CMS 12 vs SaaS**: In newer SaaS versions of Optimizely CMS, there is a convenience method `services.TryAddProtectedShellModule("ModuleName")` in the `EPiServer.DependencyInjection` namespace. This method does not exist in CMS 12. For CMS 12 compatibility, use the `Configure<ProtectedModuleOptions>` approach shown above.

---

## Packaging a Module as a NuGet Package

### Directory Structure

Your project should produce a zip archive containing the module's client resources and manifest:

```text
MyCompany.MyModule/
├── ClientResources/
│   ├── scripts/
│   │   └── initializer.js
│   └── styles/
│       └── my-styles.css
├── module.config
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── MyCompany.MyModule.csproj
```

### .csproj Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0</TargetFrameworks>
    <PackageId>MyCompany.MyModule</PackageId>
    <Version>1.0.0</Version>
    <!-- Prevent ClientResources from being compiled as content -->
    <EnableDefaultContentItems>false</EnableDefaultContentItems>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="EPiServer.CMS.Core" Version="12.23.1" />
    <PackageReference Include="EPiServer.CMS.UI.Core" Version="12.34.1" />
  </ItemGroup>

  <!-- Create zip containing ClientResources + module.config -->
  <PropertyGroup>
    <ModuleName>MyCompany.MyModule</ModuleName>
    <ModuleZipPath>$(IntermediateOutputPath)$(ModuleName).zip</ModuleZipPath>
  </PropertyGroup>

  <Target Name="CreateModuleZip"
          BeforeTargets="GenerateNuspec"
          DependsOnTargets="Build">
    <MakeDir Directories="$(IntermediateOutputPath)_module" />
    <ItemGroup>
      <ModuleFiles Include="ClientResources\**\*.*" />
      <ModuleFiles Include="module.config" />
    </ItemGroup>
    <Copy SourceFiles="@(ModuleFiles)"
          DestinationFiles="@(ModuleFiles->'$(IntermediateOutputPath)_module\%(Identity)')" />
    <ZipDirectory SourceDirectory="$(IntermediateOutputPath)_module"
                  DestinationFile="$(ModuleZipPath)"
                  Overwrite="true" />
    <RemoveDir Directories="$(IntermediateOutputPath)_module" />
  </Target>

  <!-- Pack module files into NuGet contentFiles -->
  <ItemGroup>
    <None Include="$(ModuleZipPath)"
          Pack="true"
          PackagePath="contentFiles\any\any\modules\_protected\$(ModuleName)"
          Visible="false" />
    <None Include="module.config"
          Pack="true"
          PackagePath="contentFiles\any\any\modules\_protected\$(ModuleName)"
          Visible="false" />
  </ItemGroup>

  <!-- Include .targets for consuming projects -->
  <ItemGroup>
    <None Include="$(ModuleName).targets"
          Pack="true"
          PackagePath="build\net8.0\"
          Visible="false" />
    <None Include="$(ModuleName).targets"
          Pack="true"
          PackagePath="buildTransitive\net8.0\"
          Visible="false" />
  </ItemGroup>

  <PropertyGroup>
    <ContentTargetFolders>contentFiles</ContentTargetFolders>
  </PropertyGroup>
</Project>
```

### .targets File

Create a `MyCompany.MyModule.targets` file to copy the module files to the consuming project's output:

```xml
<Project>
  <Target Name="CopyMyModuleFiles"
          AfterTargets="Build"
          Condition="'$(DesignTimeBuild)' != 'true'">
    <PropertyGroup>
      <_ModuleName>MyCompany.MyModule</_ModuleName>
      <_SourceDir>$(MSBuildThisFileDirectory)..\..\contentFiles\any\any\modules\_protected\$(_ModuleName)\</_SourceDir>
      <_DestDir>$(MSBuildProjectDirectory)\modules\_protected\$(_ModuleName)\</_DestDir>
    </PropertyGroup>
    <ItemGroup>
      <_ModuleFiles Include="$(_SourceDir)**\*.*" />
    </ItemGroup>
    <Copy SourceFiles="@(_ModuleFiles)"
          DestinationFiles="@(_ModuleFiles->'$(_DestDir)%(RecursiveDir)%(Filename)%(Extension)')"
          SkipUnchangedFiles="true" />
  </Target>
</Project>
```

### What the NuGet Package Contains

After packing, the NuGet package structure should be:

```text
MyCompany.MyModule.1.0.0.nupkg/
├── lib/net8.0/
│   └── MyCompany.MyModule.dll
├── contentFiles/any/any/modules/_protected/MyCompany.MyModule/
│   ├── MyCompany.MyModule.zip     ← zip with ClientResources/ and module.config
│   └── module.config              ← standalone copy for fallback
├── build/net8.0/
│   └── MyCompany.MyModule.targets ← copies module files to consuming project
└── buildTransitive/net8.0/
    └── MyCompany.MyModule.targets ← same, for transitive package references
```

### How the Consuming Project Uses It

In the consuming project's `Startup.cs`:

```csharp
services.AddMyModule();
```

This single call:

1. Registers the module in `ProtectedModuleOptions`
2. Registers any required services

The `.targets` file ensures the zip and `module.config` are copied to `modules/_protected/MyCompany.MyModule/` at build time, where the CMS can discover them.

---

## Common Pitfalls

### 1. Module Not Registered in ProtectedModuleOptions

**Symptom**: No errors, no 404s, but the module's initializer never runs and API endpoints return 404.

**Cause**: `ProtectedModuleOptions.AutoDiscovery` defaults to `Minimal`, so protected modules are never auto-discovered. Unlike public modules, your module won't be found just by placing files in the right directory.

**Fix**: Add `services.Configure<ProtectedModuleOptions>(...)` to register the module explicitly.

### 2. Missing clientResourceRelativePath in module.config

**Symptom**: 404 errors for all client resources. The browser requests `/EPiServer/MyModule/scripts/initializer.js` but the file is actually at `ClientResources/scripts/initializer.js` inside the zip.

**Cause**: Without `clientResourceRelativePath="ClientResources"`, the CMS sets `ClientResourcePath = ResourceBasePath` (the module root). Dojo paths resolve to `/EPiServer/MyModule/scripts/` instead of `/EPiServer/MyModule/ClientResources/scripts/`.

**Fix**: Add `clientResourceRelativePath="ClientResources"` to the `<module>` element - or restructure your zip so scripts are at the root level (not recommended).

### 3. Zip File Naming Convention

**Symptom**: Module not found even though files are in the correct directory.

**Cause**: `ZipArchiveFinder` expects the zip to be at exactly `modules/_protected/{ModuleName}/{ModuleName}.zip`. The zip filename must match the directory name exactly (case-sensitive on some systems).

**Fix**: Ensure the zip name matches the module directory name exactly.

### 4. Race Conditions in Client-Side Initializers

**Symptom**: Module sometimes works, sometimes doesn't. Data from API calls is `null` when the initializer runs.

**Cause**: Dojo `require()` inside `initialize()` is asynchronous. If you also make XHR calls, you may have multiple async operations completing in unpredictable order.

**Fix**: Use top-level `define()` dependencies instead of dynamic `require()` inside `initialize()`. For XHR calls, create a shared promise and consume it in callbacks:

```javascript
define([
    "dojo/aspect",
    "dojo/request/xhr",
    "epi-cms/contentediting/editors/ContentAreaEditor"
], function (aspect, xhr, ContentAreaEditor) {
    var dataPromise = xhr("/api/my-endpoint", { handleAs: "json" });

    return {
        initialize: function () {
            aspect.after(
                ContentAreaEditor.prototype,
                "postCreate",
                function () {
                    var editor = this;
                    dataPromise.then(function (data) {
                        // Safe: data is guaranteed to be loaded
                    });
                }
            );
        }
    };
});
```

### 5. REST Store Returns 404

**Symptom**: The REST store endpoint returns 404, even though the module is registered and static files (CSS, JS) are served correctly.

**Cause**: Optimizely's `ModuleInitializer.RegisterRestStores()` creates conventional routes that map HTTP verbs to action methods by name — `GET` maps to an action called `Get`. If your controller uses a different action name (e.g. `GetAll()`) or uses `[HttpGet]` / `[HttpGet("{id}")]` attributes, the action either won't match the conventional route or the attribute routing will opt it out of conventional routing entirely.

**Fix**: Follow the built-in `DisplayOptionsStore` pattern — use a single `Get(string id)` method with no `[HttpGet]` attributes:

```csharp
[RestStore("mystorename")]
public class MyStore : RestControllerBase
{
    // Correct: single Get method, no [HttpGet] attribute
    public IActionResult Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Rest(allItems);

        var item = FindById(id);
        return item != null ? Rest(item) : NotFound();
    }
}
```

### 6. EPiServer.Shell.dll Not Found

**Symptom**: Build error - `ProtectedModuleOptions`, `ModuleDetails`, or other types in `EPiServer.Shell.Modules` namespace cannot be found.

**Cause**: `EPiServer.Shell.dll` ships inside the `EPiServer.CMS.UI.Core` NuGet package, not in `EPiServer.CMS.Core` or `EPiServer.Framework`.

**Fix**: Add `EPiServer.CMS.UI.Core` as a package reference.

---

## Key Namespaces and Types

| Type                     | Namespace                       | NuGet Package           |
| ------------------------ | ------------------------------- | ----------------------- |
| `ProtectedModuleOptions` | `EPiServer.Shell.Modules`       | `EPiServer.CMS.UI.Core` |
| `PublicModuleOptions`    | `EPiServer.Shell.Modules`       | `EPiServer.CMS.UI.Core` |
| `ModuleDetails`          | `EPiServer.Shell.Modules`       | `EPiServer.CMS.UI.Core` |
| `ModuleTable`            | `EPiServer.Shell.Modules`       | `EPiServer.CMS.UI.Core` |
| `ShellModule`            | `EPiServer.Shell.Modules`       | `EPiServer.CMS.UI.Core` |
| `ShellModuleManifest`    | `EPiServer.Shell.Configuration` | `EPiServer.CMS.UI.Core` |
| `ConfigModuleProvider`   | `EPiServer.Shell.Modules`       | `EPiServer.CMS.UI.Core` |
| `ModuleFinder`           | `EPiServer.Shell.Modules`       | `EPiServer.CMS.UI.Core` |
