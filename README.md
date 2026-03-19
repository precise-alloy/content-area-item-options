# TuyenPham.ContentAreaItemOptions

An Optimizely CMS plugin that adds custom option selectors (theme, margin, padding, etc.) to content area items in the editor UI.

Editors can pick options from dropdown selectors on each content area block, and the selected values are persisted as render settings — ready for your content area renderer to apply as CSS classes or any other rendering logic.

## Features

- Define unlimited custom selectors (theme, margin, padding, …) with a simple fluent API
- Options appear automatically in the content area item context menu
- Restrict which options are available per block type using attributes
- Selected values are stored in `ContentAreaItem.RenderSettings` and accessible during rendering
- Ships as a single NuGet package — no manual file copying required

## Installation

```shell
dotnet add package TuyenPham.ContentAreaItemOptions
```

Or via the NuGet Package Manager:

```powershell
Install-Package TuyenPham.ContentAreaItemOptions
```

## Setup

### 1. Define Your Options

Create an extension method (or add to an existing one) that builds a `ContentAreaItemOptionsRegistry` and calls `AddContentAreaItemOptions()`:

```csharp
using TuyenPham.ContentAreaItemOptions.DependencyInjection;
using TuyenPham.ContentAreaItemOptions.Models;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterContentAreaItemOptions(
        this IServiceCollection services)
    {
        var registry = new ContentAreaItemOptionsRegistry
        {
            new ContentAreaItemOptions
            {
                AttributeName = "data-custom-theme",
                SelectorName = "theme",
                LabelPrefix = "Theme",
            }
            .Add(new ContentAreaItemOption { Id = "black", Name = "Black", CssClass = "theme-black" })
            .Add(new ContentAreaItemOption { Id = "white", Name = "White", CssClass = "theme-white" })
            .Add(new ContentAreaItemOption { Id = "blue",  Name = "Blue",  CssClass = "theme-blue" }),

            new ContentAreaItemOptions
            {
                AttributeName = "data-custom-margin",
                SelectorName = "margin",
                LabelPrefix = "Margin",
            }
            .Add(new ContentAreaItemOption { Id = "top",    Name = "Top",    CssClass = "margin-top" })
            .Add(new ContentAreaItemOption { Id = "bottom", Name = "Bottom", CssClass = "margin-bottom" })
            .Add(new ContentAreaItemOption { Id = "both",   Name = "Both",   CssClass = "margin-both" })
            .Add(new ContentAreaItemOption { Id = "none",   Name = "None",   CssClass = "margin-none" }),
        };

        services.AddContentAreaItemOptions(registry);

        return services;
    }
}
```

> **Important**: `AttributeName` values **must** start with `data-` to be persisted in `ContentAreaItem.RenderSettings` by the CMS.

#### ContentAreaItemOptions Properties

| Property        | Description                                                                                                                                                                |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AttributeName` | The render setting key (must start with `data-`). Used to store and retrieve the selected value.                                                                           |
| `SelectorName`  | A unique identifier for the selector. Also used as the id when fetching a single selector from the REST store.                                                             |
| `LabelPrefix`   | Label shown in the editor context menu (e.g. `"Theme"` → displays `"Theme: Blue"`).                                                                                        |
| `DefaultLabel`  | Label when no option is selected. Default: `"Default"`.                                                                                                                    |
| `Availability`  | Controls default visibility. `All` (default): shown for all content types. `Specific`: only shown for content types with an explicit `[ContentAreaItemOptions]` attribute. |

#### ContentAreaItemOption Properties

| Property      | Description                                                                    |
| ------------- | ------------------------------------------------------------------------------ |
| `Id`          | Unique identifier for the option (stored in render settings).                  |
| `Name`        | Display name shown to editors.                                                 |
| `Description` | Optional description/tooltip.                                                  |
| `CssClass`    | CSS class to apply during rendering (optional — you control how this is used). |
| `IconClass`   | Optional CSS class for an icon in the selector UI.                             |

### 2. Register in Startup

Call your extension method in `ConfigureServices`:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // ... other services ...

    services.RegisterContentAreaItemOptions();
}
```

### 3. Apply Options During Rendering

Override `ContentAreaRenderer` to read the selected values from render settings and apply them. Here's an example that collects CSS classes from all selectors:

```csharp
using EPiServer.Web.Mvc.Html;
using TuyenPham.ContentAreaItemOptions.Models;

public class CustomContentAreaRenderer : ContentAreaRenderer
{
    private readonly ContentAreaItemOptionsRegistry _optionsRegistry;

    public CustomContentAreaRenderer(ContentAreaItemOptionsRegistry optionsRegistry)
    {
        _optionsRegistry = optionsRegistry;
    }

    protected override void RenderContentAreaItem(
        IHtmlHelper htmlHelper,
        ContentAreaItem contentAreaItem,
        string templateTag,
        string htmlTag,
        string cssClass)
    {
        var renderSettings = contentAreaItem.RenderSettings
            ?? new Dictionary<string, object>();

        var customClasses = GetCustomCssClasses(renderSettings);

        // Pass classes to your view via ViewBag, htmlTag, or however you render blocks
        htmlHelper.ViewBag.CustomCssClasses = customClasses;

        base.RenderContentAreaItem(htmlHelper, contentAreaItem, templateTag, htmlTag, cssClass);
    }

    private string GetCustomCssClasses(IDictionary<string, object> renderSettings)
    {
        var classes = new List<string>();

        foreach (var selector in _optionsRegistry)
        {
            if (renderSettings.TryGetValue(selector.AttributeName, out var value)
                && value is string id
                && selector.Get(id) is { CssClass: not null } option)
            {
                classes.Add(option.CssClass);
            }
        }

        return string.Join(" ", classes);
    }
}
```

Register the custom renderer in `ConfigureServices`:

```csharp
services.AddTransient<ContentAreaRenderer, CustomContentAreaRenderer>();
```

## Restricting Options Per Block Type

You can restrict which options appear for specific block types using the `[ContentAreaItemOptions]` attribute.

The behavior depends on the selector's `Availability` setting:

### `Availability = All` (default)

All content types see the selector by default. Use the attribute to restrict or hide it:

```csharp
using TuyenPham.ContentAreaItemOptions.Models;

// Only show "black" and "white" themes for this block
[ContentAreaItemOptions("data-custom-theme", "black", "white")]
public class HeroBlock : BlockData
{
    // ...
}

// Hide the margin selector entirely for this block (no option IDs = hidden)
[ContentAreaItemOptions("data-custom-margin")]
public class BannerBlock : BlockData
{
    // ...
}
```

| Usage                                                             | Effect                                           |
| ----------------------------------------------------------------- | ------------------------------------------------ |
| `[ContentAreaItemOptions("data-custom-theme", "black", "white")]` | Only "black" and "white" options are shown       |
| `[ContentAreaItemOptions("data-custom-theme")]`                   | The theme selector is hidden for this block type |
| No attribute                                                      | All options are shown (default behavior)         |

### `Availability = Specific`

The selector is hidden by default. Only content types with an explicit `[ContentAreaItemOptions]` attribute will see it:

```csharp
var registry = new ContentAreaItemOptionsRegistry
{
    new ContentAreaItemOptions
    {
        AttributeName = "data-custom-layout",
        SelectorName = "layout",
        LabelPrefix = "Layout",
        Availability = ContentAreaItemOptionsAvailability.Specific,
    }
    .Add(new ContentAreaItemOption { Id = "wide", Name = "Wide", CssClass = "layout-wide" })
    .Add(new ContentAreaItemOption { Id = "narrow", Name = "Narrow", CssClass = "layout-narrow" }),
};
```

```csharp
// This block opts in to the layout selector with only "wide"
[ContentAreaItemOptions("data-custom-layout", "wide")]
public class ArticleBlock : BlockData { /* ... */ }

// This block has no attribute → layout selector is hidden
public class PromoBlock : BlockData { /* ... */ }
```

| Usage                                                    | Effect                                            |
| -------------------------------------------------------- | ------------------------------------------------- |
| `[ContentAreaItemOptions("data-custom-layout", "wide")]` | Only "wide" option is shown                       |
| `[ContentAreaItemOptions("data-custom-layout")]`         | The layout selector is hidden for this block type |
| No attribute                                             | The layout selector is hidden (Specific mode)     |

The attribute can be applied multiple times on the same class, once per selector.

## REST Store Endpoint

The package exposes an authorized REST store endpoint via Optimizely's `[RestStore]` convention:

- `GET /EPiServer/TuyenPham.ContentAreaItemOptions/Stores/content-area-options/` — Returns all selectors with their options and per-content-type restrictions
- `GET /EPiServer/TuyenPham.ContentAreaItemOptions/Stores/content-area-options/{selectorName}` — Returns a single selector

The client-side initializer uses the `epi.storeregistry` to call this endpoint automatically — you don't need to interact with it directly. It's mentioned here for debugging purposes.

## How It Works

1. At startup, `AddContentAreaItemOptions()` registers the module in `ProtectedModuleOptions` so the CMS discovers its client-side resources and REST store
2. When an editor opens the CMS UI, the Dojo initializer registers a `content-area-options` store via `epi.storeregistry` and fetches all selectors from the REST store endpoint
3. For each selector, a command is added to `ContentAreaEditor`'s context menu
4. When the editor selects an option, the value is saved in the content area item's render settings under the `AttributeName` key
5. During rendering, your `ContentAreaRenderer` reads the value and applies it (e.g. as a CSS class)

## Requirements

- Optimizely CMS 12 (EPiServer.CMS.Core 12.23.1+)
- .NET 8.0+
