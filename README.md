# TuyenPham.ContentAreaItemOptions

An Optimizely CMS plugin that adds custom option selectors (theme, margin, padding, etc.) to content area items in the editor UI.

Editors can pick options from dropdown selectors on each content area block, and the selected values are persisted as render settings — ready for your content area renderer to apply as CSS classes or any other rendering logic.

## Features

- Define unlimited custom selectors (theme, margin, padding, …) with a simple fluent API
- Options appear automatically in the content area item context menu
- Restrict which options are available per block type using attributes
- Enable selectors for all items in a specific content area using attributes on the property
- Selected values are stored in `ContentAreaItem.RenderSettings` and accessible during rendering
- Ships as a single NuGet package — no manual file copying required

## Installation

Install from [nuget.org](https://www.nuget.org/packages/TuyenPham.ContentAreaItemOptions) or the [Optimizely NuGet feed](https://nuget.optimizely.com/packages/tuyenpham.contentareaitemoptions):

```shell
dotnet add package TuyenPham.ContentAreaItemOptions
```

Or via the NuGet Package Manager:

```powershell
Install-Package TuyenPham.ContentAreaItemOptions
```

Build from [source](https://github.com/precise-alloy/content-area-item-options):

```bash
git clone https://github.com/precise-alloy/content-area-item-options.git content-area-item-options
cd content-area-item-options
dotnet build
```

Run the tests:

```bash
dotnet run --project TuyenPham.ContentAreaItemOptions.Tests   # server-side
bun test                                                      # client-side
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

`AddContentAreaItemOptions` validates the registry at startup and throws an `ArgumentException` listing **every** problem it finds:

- `AttributeName` must be non-empty and start with `data-` — the CMS only persists render settings with that prefix
- `SelectorName` must be non-empty
- `AttributeName` and `SelectorName` must each be unique across the registry (case-insensitive)
- Option `Id` must be non-empty, and unique within its selector (case-insensitive)

#### ContentAreaItemOptions Properties

| Property        | Description                                                                                                                                                       |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AttributeName` | The render setting key (must start with `data-`). Used to store and retrieve the selected value.                                                                  |
| `SelectorName`  | A unique identifier for the selector. Also used as the id when fetching a single selector from the REST store.                                                    |
| `LabelPrefix`   | Label shown in the editor context menu (e.g. `"Theme"` → displays `"Theme: Blue"`).                                                                               |
| `DefaultLabel`  | Label when no option is selected. Default: `"Default"`.                                                                                                          |
| `Availability`  | Controls default visibility. See the table below.                                                                                                                |

#### Availability Values

| Value      | Effect                                                                                                                          |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------- |
| `All`      | Default. Shown for all content types unless an attribute restricts or hides it.                                                 |
| `Specific` | Hidden unless a content type or ContentArea property explicitly opts in with `[ContentAreaItemOptions]`.                        |
| `None`     | Hidden everywhere. Attributes cannot opt back in, and values already stored in render settings are ignored during rendering.    |

Use `None` to retire a selector without deleting its definition or its persisted data. Switching back to `All` or `Specific` restores the previously selected values.

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

Override `ContentAreaRenderer` to read the selected values from render settings and apply them. `GetApplicableCssClasses` validates that each option is still applicable — checking content-type restrictions, ContentArea property overrides, and the `Availability` setting — so stale render settings left behind after a selector was hidden or restricted are ignored.

```csharp
using EPiServer.Core;
using EPiServer.Web;
using EPiServer.Web.Mvc.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using TuyenPham.ContentAreaItemOptions.Infrastructure;
using TuyenPham.ContentAreaItemOptions.Models;

public class CustomContentAreaRenderer : ContentAreaRenderer
{
    private readonly ContentAreaItemOptionsRegistry _optionsRegistry;
    private readonly ContentAreaItemOptionsRestrictionResolver _restrictionResolver;
    private readonly IContentAreaLoader _contentAreaLoader;

    // Per-render state; the renderer must be registered as Transient (see below).
    private Dictionary<string, string[]?>? _propertyOverrides;

    public CustomContentAreaRenderer(
        ContentAreaItemOptionsRegistry optionsRegistry,
        ContentAreaItemOptionsRestrictionResolver restrictionResolver,
        IContentAreaLoader contentAreaLoader)
    {
        _optionsRegistry = optionsRegistry;
        _restrictionResolver = restrictionResolver;
        _contentAreaLoader = contentAreaLoader;
    }

    public override void Render(IHtmlHelper htmlHelper, ContentArea contentArea)
    {
        // Nested content areas re-enter Render, so the outer value has to be restored.
        var previous = _propertyOverrides;
        _propertyOverrides = ContentAreaItemOptionsMetadataExtender
            .GetPropertyOverrides(htmlHelper.ViewData.ModelMetadata);

        try
        {
            base.Render(htmlHelper, contentArea);
        }
        finally
        {
            _propertyOverrides = previous;
        }
    }

    protected override void RenderContentAreaItem(
        IHtmlHelper htmlHelper,
        ContentAreaItem contentAreaItem,
        string templateTag,
        string htmlTag,
        string cssClass)
    {
        // LoadContent also resolves inline blocks, which have no ContentLink.
        var contentTypeId = (_contentAreaLoader.LoadContent(contentAreaItem) as ContentData)?.ContentTypeID;

        var optionClasses = _restrictionResolver.GetApplicableCssClasses(
            _optionsRegistry,
            contentAreaItem.RenderSettings,
            contentTypeId,
            _propertyOverrides);

        base.RenderContentAreaItem(
            htmlHelper,
            contentAreaItem,
            templateTag,
            htmlTag,
            string.Join(" ", new[] { cssClass, optionClasses }.Where(c => !string.IsNullOrWhiteSpace(c))));
    }
}
```

Register the custom renderer in `ConfigureServices`:

```csharp
services.AddTransient<ContentAreaRenderer, CustomContentAreaRenderer>();
```

> **Register it as `Transient`.** The CMS default is `TryAddSingleton<ContentAreaRenderer>()`. `_propertyOverrides` is mutable instance state, so a singleton registration would leak overrides across concurrent requests. If you prefer a singleton renderer, pass the overrides through `htmlHelper.ViewData` instead of a field.

`_propertyOverrides` is only needed when you use `[ContentAreaItemOptions]` or `[HideContentAreaItemOptions]` on a ContentArea **property**. If you only use them on block classes, you can pass `null` and drop the `Render` override.

If you would rather apply the classes inside your block views, assign `optionClasses` to `htmlHelper.ViewData` or `ViewBag` instead of appending to `cssClass`.

## Controlling Options with `[ContentAreaItemOptions]` and `[HideContentAreaItemOptions]`

The `[ContentAreaItemOptions]` attribute can be applied to **block classes** (to enable or restrict options per block type) or to **ContentArea properties** (to enable selectors for all items in that content area).

To **hide** a selector, use the separate `[HideContentAreaItemOptions]` attribute.

The behavior depends on the selector's `Availability` setting.

### `Availability = All` (default)

All content types see the selector by default. Use the attributes to restrict or hide it:

```csharp
using TuyenPham.ContentAreaItemOptions.Models;

// Only show "black" and "white" themes for this block
[ContentAreaItemOptions("data-custom-theme", "black", "white")]
public class HeroBlock : BlockData
{
    // ...
}

// Hide the margin selector entirely for this block
[HideContentAreaItemOptions("data-custom-margin")]
public class BannerBlock : BlockData
{
    // ...
}
```

| Usage                                                             | Effect                                           |
| ----------------------------------------------------------------- | ------------------------------------------------ |
| `[ContentAreaItemOptions("data-custom-theme", "black", "white")]` | Only "black" and "white" options are shown       |
| `[ContentAreaItemOptions("data-custom-theme")]`                   | All options are enabled (same as no attribute)   |
| `[HideContentAreaItemOptions("data-custom-theme")]`               | The theme selector is hidden for this block type |
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
// This block opts in to the layout selector with all options
[ContentAreaItemOptions("data-custom-layout")]
public class ArticleBlock : BlockData { /* ... */ }

// This block opts in to the layout selector with only "wide"
[ContentAreaItemOptions("data-custom-layout", "wide")]
public class FeatureBlock : BlockData { /* ... */ }

// This block has no attribute → layout selector is hidden
public class PromoBlock : BlockData { /* ... */ }
```

| Usage                                                    | Effect                                            |
| -------------------------------------------------------- | ------------------------------------------------- |
| `[ContentAreaItemOptions("data-custom-layout")]`         | All layout options are enabled                    |
| `[ContentAreaItemOptions("data-custom-layout", "wide")]` | Only "wide" option is shown                       |
| `[HideContentAreaItemOptions("data-custom-layout")]`     | The layout selector is hidden for this block type |
| No attribute                                             | The layout selector is hidden (Specific mode)     |

The attributes can be applied multiple times on the same class, once per selector, and are inherited by derived block types.

Attribute names are matched case-insensitively. Repeating `[ContentAreaItemOptions]` for the same selector combines its option IDs; an occurrence without option IDs enables every option. `[HideContentAreaItemOptions]` always wins when it targets the same selector.

### `Availability = None`

The selector is hidden for every block type and every content area, and attributes are ignored:

```csharp
new ContentAreaItemOptions
{
    AttributeName = "data-custom-layout",
    SelectorName = "layout",
    LabelPrefix = "Layout",
    Availability = ContentAreaItemOptionsAvailability.None,
}
```

Values already stored in render settings stay in the database but are never returned by `GetApplicableCssClasses`.

### Enabling Options on a ContentArea Property

Instead of (or in addition to) placing the attribute on each block class, you can apply it to a `ContentArea` property. This enables the selector for **all items** placed in that content area, regardless of block type. This is especially useful with `Availability = Specific`.

You can also use `[HideContentAreaItemOptions]` on a ContentArea property to hide a selector for all items in that area.

```csharp
using TuyenPham.ContentAreaItemOptions.Models;

public class StartPage : PageData
{
    // Enable the layout selector for all items in this content area (all options)
    [ContentAreaItemOptions("data-custom-layout")]
    public virtual ContentArea MainContentArea { get; set; }

    // Enable with only specific options
    [ContentAreaItemOptions("data-custom-layout", "wide")]
    public virtual ContentArea SidebarContentArea { get; set; }

    // Hide the theme selector for all items in this content area
    [HideContentAreaItemOptions("data-custom-theme")]
    public virtual ContentArea PromoContentArea { get; set; }

    // No attribute → layout selector stays hidden (Specific mode)
    public virtual ContentArea FooterContentArea { get; set; }
}
```

| Usage on ContentArea property                            | Effect                                                |
| -------------------------------------------------------- | ----------------------------------------------------- |
| `[ContentAreaItemOptions("data-custom-layout")]`         | All layout options are shown for items in this area   |
| `[ContentAreaItemOptions("data-custom-layout", "wide")]` | Only "wide" is shown for items in this area           |
| `[HideContentAreaItemOptions("data-custom-layout")]`     | The layout selector is hidden for items in this area  |
| No attribute                                             | Falls back to block-type rules / Availability setting |

> **Property-level attributes only affect rendering if you wire them up.** The editor UI picks them up automatically, but `GetApplicableCssClasses` needs `_propertyOverrides` passed in as shown in [Apply Options During Rendering](#3-apply-options-during-rendering). Without that, a selector hidden on a ContentArea property disappears from the editor while stale values keep rendering.

### Precedence

1. **`Availability = None`** — hidden unconditionally, nothing can override it
2. **Content type (block class)** — `[ContentAreaItemOptions]` / `[HideContentAreaItemOptions]` on the block type
3. **ContentArea property** — the same attributes on the `ContentArea` property
4. **Global** — the selector's `Availability` setting (`All` or `Specific`)

If a block type has its own attribute for a selector, that restriction applies even if the ContentArea property enables all options. The same chain is enforced in the editor UI (`content-area-item-command.js`) and during rendering (`GetApplicableCssClasses` / `IsOptionApplicable`), and both implementations are covered by the test suites.

When the content type of an item cannot be resolved (for example an inline block loaded without `IContentAreaLoader`), step 2 is skipped; steps 1, 3 and 4 still apply.

## REST Store Endpoint

The package exposes an authorized REST store endpoint via Optimizely's `[RestStore]` convention. The route is protected by the CMS shell module authorization and requires an antiforgery token, which the Dojo client sends automatically:

- `GET /EPiServer/TuyenPham.ContentAreaItemOptions/Stores/content-area-options/` — Returns all selectors with their options and per-content-type restrictions
- `GET /EPiServer/TuyenPham.ContentAreaItemOptions/Stores/content-area-options/{selectorName}` — Returns a single selector, in the same shape as one list entry

The client-side initializer uses the `epi.storeregistry` to call this endpoint automatically — you don't need to interact with it directly. It's mentioned here for debugging purposes.

## How It Works

1. At startup, `AddContentAreaItemOptions()` validates the registry and registers the module in `ProtectedModuleOptions` so the CMS discovers its client-side resources and REST store
2. When an editor opens the CMS UI, the Dojo initializer registers a store via `epi.storeregistry` and fetches all selectors from the REST store endpoint once per session
3. For each selector, a command is added to `ContentAreaEditor`'s context menu
4. When the editor selects an option, the value is saved in the content area item's render settings under the `AttributeName` key
5. During rendering, your `ContentAreaRenderer` reads the value and applies it (e.g. as a CSS class)

## Testing

Server-side tests use [xUnit.net v3](https://xunit.net/); client-side tests run on [Bun](https://bun.sh/) against the shipped Dojo modules through a minimal AMD shim.

- **Models** — `ContentAreaItemOption`, `ContentAreaItemOptions`, `ContentAreaItemOptionsRegistry`, attributes, and the `Availability` enum
- **Infrastructure** — `ContentAreaItemOptionsRestrictionResolver` (full precedence matrix including `None` and unresolved content types), `ContentAreaOptionsStore` (serialized JSON payload), and `ContentAreaItemOptionsMetadataExtender`
- **DI registration** — service registration, `ProtectedModuleOptions`, and every registry validation rule
- **Client** — `content-area-item-command.js` precedence, availability and labelling, mirroring the server-side matrix

```bash
dotnet run --project TuyenPham.ContentAreaItemOptions.Tests
bun test
```

## Change logs

### v4.0.0

Breaking changes:

- `GetApplicableCssClasses` now takes `IDictionary<string, string>` to match `ContentAreaItem.RenderSettings` in CMS 13. Pass `contentAreaItem.RenderSettings` directly; the CMS 12 `IDictionary<string, object>` shape is gone.
- `IsOptionApplicable` now takes `int? contentTypeId`. A `null` content type no longer disables all checks — property overrides and `Availability` are still enforced.
- `AddContentAreaItemOptions` rejects duplicate `AttributeName`/`SelectorName`/option `Id` values and empty names, in addition to the existing `data-` prefix check.

Fixes:

- `Availability = None` is now honoured by both the editor UI and rendering. It previously behaved like `All`.
- Selecting a block no longer writes `null` render settings for every selector, and the popup menu is rebuilt once per selection instead of twice.
- The command label now updates from an explicit callback instead of a `dojo/Stateful` watch that could never fire.
- Removed the unreachable `apiUrl` fallback, which could not satisfy the store's antiforgery requirement.
- The client store is registered under a namespaced key so it cannot collide with other modules, and a failed fetch degrades to "no selectors" instead of hanging.
- `dotnet pack` no longer requires PowerShell, so it works on a clean Linux or macOS machine.
- CI publishes to NuGet only on pushes to `release`, never from pull requests.

Additions:

- `ContentAreaItemOptionsMetadataExtender.GetPropertyOverrides(ModelMetadata)` for use in `ContentAreaRenderer.Render`.
- The single-selector REST response now returns the same shape as a list entry.

### v3.0.0

- Update to .NET 10 and CMS 13

## Requirements

From version 3.0.0:

- Optimizely CMS 13 (`EPiServer.CMS.UI.Core` 13.1.1+)
- .NET 10.0+

For older versions:

- Optimizely CMS 12 (`EPiServer.CMS.UI.Core` 12.23.1+)
- .NET 8.0+

## License

Apache License, version 2.0
