# Optimizely CMS: Display Options - Architecture & Implementation Guide

This document explains how **Display Options** are implemented in Optimizely CMS, and how site developers can follow the same pattern to build custom per-item selectors (theme, margin, padding, image position, etc.) for content area items.

## Table of Contents

- [Optimizely CMS: Display Options - Architecture \& Implementation Guide](#optimizely-cms-display-options---architecture--implementation-guide)
    - [Table of Contents](#table-of-contents)
    - [Overview](#overview)
    - [Architecture Diagram](#architecture-diagram)
    - [C# Server-Side Implementation](#c-server-side-implementation)
        - [1. The Model - `DisplayOption`](#1-the-model---displayoption)
        - [2. The Registry - `DisplayOptions`](#2-the-registry---displayoptions)
        - [3. Registration at Startup](#3-registration-at-startup)
        - [4. Storage on `ContentAreaItem`](#4-storage-on-contentareaitem)
        - [5. REST API Store - `DisplayOptionsStore`](#5-rest-api-store---displayoptionsstore)
        - [6. Template Resolution - `DisplayOptionsModelTemplateTagProvider`](#6-template-resolution---displayoptionsmodeltemplatetagprovider)
        - [7. Rendering - `ContentAreaRenderer`](#7-rendering---contentarearenderer)
        - [8. Edit-Mode HTML Attributes - `DefaultContentAreaItemAttributeAssembler`](#8-edit-mode-html-attributes---defaultcontentareaitemattributeassembler)
    - [Dojo Client-Side Implementation](#dojo-client-side-implementation)
        - [1. Store Registration - `CMSModule.js`](#1-store-registration---cmsmodulejs)
        - [2. View Model - `ContentBlockViewModel.js`](#2-view-model---contentblockviewmodeljs)
        - [3. Command - `SelectDisplayOption.js`](#3-command---selectdisplayoptionjs)
        - [4. Popup Widget - `DisplayOptionSelector.js`](#4-popup-widget---displayoptionselectorjs)
        - [5. Wiring Into Content Area Editors](#5-wiring-into-content-area-editors)
    - [Data Flow Summary](#data-flow-summary)
        - [Editor Selects a Display Option](#editor-selects-a-display-option)
        - [Page Renders with Display Option](#page-renders-with-display-option)
    - [Building a Custom Selector (e.g. Theme, Margin)](#building-a-custom-selector-eg-theme-margin)
        - [Step-by-Step C# Implementation](#step-by-step-c-implementation)
            - [1. Define Your Attribute Name Constant](#1-define-your-attribute-name-constant)
            - [2. Create Your Option Model](#2-create-your-option-model)
            - [3. Create a Registry](#3-create-a-registry)
            - [4. Register Options at Startup](#4-register-options-at-startup)
            - [5. Create a REST Store](#5-create-a-rest-store)
            - [6. Read During Rendering](#6-read-during-rendering)
        - [Step-by-Step Dojo Implementation](#step-by-step-dojo-implementation)
            - [1. Register the Store in a Module Initializer](#1-register-the-store-in-a-module-initializer)
            - [2. Create the Selector Widget](#2-create-the-selector-widget)
            - [3. Create the Command](#3-create-the-command)
            - [4. Wire the Command Into the Content Area Editor](#4-wire-the-command-into-the-content-area-editor)
        - [Reading Custom Settings During Rendering](#reading-custom-settings-during-rendering)
    - [Key Conventions and Constraints](#key-conventions-and-constraints)

## Overview

**Display Options** allow editors to choose how each content block renders inside a `ContentArea`. For example, a block can be displayed as "Full width", "Half width", or "One-third width" - without changing the block content itself. The same block type can appear in multiple content areas with different display options per placement.

The system works through these interconnected layers:

| Layer                   | Technology | Responsibility                                                                        |
| ----------------------- | ---------- | ------------------------------------------------------------------------------------- |
| **Model**               | C#         | `DisplayOption` class - id, name, tag, icon                                           |
| **Registry**            | C#         | `DisplayOptions` singleton - registry of all available options                        |
| **Storage**             | C#         | `ContentAreaItem.RenderSettings` dictionary - persists the editor's choice            |
| **REST API**            | C#         | `DisplayOptionsStore` - serves options to the UI as JSON                              |
| **Template Resolution** | C#         | `DisplayOptionsModelTemplateTagProvider` - maps selected option to a template tag     |
| **Rendering**           | C#         | `ContentAreaRenderer` - resolves templates and wraps output in HTML with CSS classes  |
| **Edit Attributes**     | C#         | `DefaultContentAreaItemAttributeAssembler` - writes `data-*` attributes for edit mode |
| **Client Store**        | Dojo JS    | `CMSModule.js` - registers the `epi.cms.displayoptions` store                         |
| **View Model**          | Dojo JS    | `ContentBlockViewModel.js` - stores/retrieves the selected option in `attributes`     |
| **Command**             | Dojo JS    | `SelectDisplayOption.js` - the context menu command                                   |
| **Popup Widget**        | Dojo JS    | `DisplayOptionSelector.js` - the radio menu UI                                        |

---

## Architecture Diagram

```text
┌───────────────────────────────────────────────────────────────────────┐
│                         EDITOR UI (Browser)                           │
│                                                                       │
│  ┌──────────────────┐     ┌───────────────────────────────┐           │
│  │ ContentArea      │     │ Context Menu                  │           │
│  │ Editor / Overlay ├────▶│ SelectDisplayOption (command) │           │
│  └──────────────────┘     │  └─ DisplayOptionSelector     │           │
│                           │     │  (popup widget)         │           │
│                           │     ├─ Radio: Automatic       │           │
│                           │     ├─ Radio: Full            │           │
│                           │     └─ Radio: Half            │           │
│                           └────────┬──────────────────────┘           │
│                                    │                                  │
│                                    ▼                                  │
│  ┌──────────────────────────────────────────────────────────┐         │
│  │ ContentBlockViewModel                                    │         │
│  │   attributes["data-epi-content-display-option"] = "full" │         │
│  └─────────────────────────────────┬────────────────────────┘         │
│                                    │ serialize()                      │
│                                    ▼                                  │
├────────────────────────────── REST API ───────────────────────────────┤
│                                                                       │
│  GET  /displayoptions      → DisplayOptionsStore → DisplayOptions     │
│  POST /contentdata (save)  → ContentAreaItem.RenderSettings persisted │
│                                                                       │
├────────────────────────── SERVER RENDERING ───────────────────────────┤
│                                                                       │
│  ContentAreaRenderer.RenderContentAreaItem(...)                       │
│    │                                                                  │
│    ├─ LoadDisplayOption(item) → reads RenderSettings → DisplayOption  │
│    ├─ Gets Tag from DisplayOption → template tag                      │
│    ├─ ResolveContentTemplate(..., tags) → finds matching view         │
│    └─ Wraps output in <div data-epi-content-display-option="full">    │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

---

## C# Server-Side Implementation

### 1. The Model - `DisplayOption`

**File:** `EPiServer/Web/DisplayOption.cs`

```csharp
public class DisplayOption : IReadOnly<DisplayOption>
{
    public virtual string Id { get; set; }          // Unique identifier (e.g., "full", "half")
    public virtual string Name { get; set; }        // Display name (supports localization keys)
    public virtual string Description { get; set; } // Tooltip text (supports localization keys)
    public virtual string Tag { get; set; }         // Template tag for view resolution
    public virtual string IconClass { get; set; }   // CSS class for the selector icon
    public bool IsReadOnly { get; private set; }
}
```

**Key points:**

- `Id` is the primary key, stored in `ContentAreaItem.RenderSettings`.
- `Tag` is what drives template resolution - it selects which partial view renders the block.
- `Name` and `Description` support localization resource keys (e.g., `/displayoptions/full/name`).
- After initialization completes, all options are made read-only via `MakeReadOnly()`.

### 2. The Registry - `DisplayOptions`

**File:** `EPiServer/Web/DisplayOptions.cs`

A singleton `ConcurrentDictionary`-backed registry that holds all registered `DisplayOption` instances:

```csharp
public partial class DisplayOptions : IEnumerable<DisplayOption>
{
    // Add by object or by parameters
    public virtual DisplayOptions Add(DisplayOption displayOption);
    public virtual DisplayOptions Add(string id, string name, string tag, string description, string iconClass);

    // Retrieve by id
    public virtual DisplayOption Get(string id);

    // Remove by id
    public virtual void Remove(string id);

    // Enumerate in registration order
    public IEnumerator<DisplayOption> GetEnumerator();
}
```

Options are maintained in insertion order. The registry is registered as a singleton in DI.

### 3. Registration at Startup

**File:** `EPiServer/Initialization/Internal/CmsRuntimeInitialization.cs`

During CMS initialization, the framework scans all assemblies for classes that extend `DisplayOption` and auto-registers them:

```csharp
internal static void InitializeDisplayOptionsAndResolutions(IServiceProvider services)
{
    var typeScanner = services.GetRequiredService<ITypeScannerLookup>();
    var displayOptions = services.GetRequiredService<DisplayOptions>();

    foreach (var displayOption in typeScanner.AllTypes
       .Where(t => typeof(DisplayOption).IsAssignableFrom(t) && !t.IsAbstract))
    {
        displayOptions.Add(
            (DisplayOption)ActivatorUtilities.GetServiceOrCreateInstance(services, displayOption));
    }
}
```

After `InitComplete`, options are locked: `displayOptions.MakeItemsReadOnly()`.

**Site developers register options** either by:

1. Creating classes that extend `DisplayOption`, or
2. Manually calling `displayOptions.Add(id, name, tag)` in a `Startup.cs` or initialization module.

### 4. Storage on `ContentAreaItem`

**File:** `EPiServer/Core/ContentAreaItem.cs`

The selected display option is stored as a key-value pair in `ContentAreaItem.RenderSettings`:

```csharp
public virtual IDictionary<string, string> RenderSettings { get; init; }
```

The key is a well-known constant: `"data-epi-content-display-option"` (defined as `ContentFragment.ContentDisplayOptionAttributeName`).

There is an internal convenience accessor:

```csharp
internal string DisplayOptionId
{
    get => RenderSettings?.TryGetValue("data-epi-content-display-option", out var id) == true ? id : null;
    set
    {
        if (value == null)
            RenderSettings.Remove("data-epi-content-display-option");
        else
            RenderSettings.Add("data-epi-content-display-option", value);
    }
}
```

> **Important:** Only keys prefixed with `"data-"` are persisted to the database. Non-prefixed keys are treated as temporary render settings.

### 5. REST API Store - `DisplayOptionsStore`

**File:** `EPiServer.Cms.Shell.UI/UI/Rest/DisplayOptionsStore.cs`

A REST controller that serves display options to the Dojo UI:

```csharp
[RestStore("displayoptions")]
internal class DisplayOptionsStore : RestControllerBase
{
    public ActionResult Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Rest(_displayOptions.Select(o => new DisplayOptionModel(o, _localizationService)));

        var option = _displayOptions.Get(id);
        return option != null ? Rest(new DisplayOptionModel(option, _localizationService)) : NotFound();
    }
}
```

The `DisplayOptionModel` translates the server-side model to a JSON-friendly DTO, resolving localization keys for `Name` and `Description` via `LocalizationService`.

**JSON response shape:**

```json
[
  { "id": "full",  "name": "Full Width",  "description": "...", "tag": "Full",  "iconClass": "epi-icon__layout--full" },
  { "id": "half",  "name": "Half Width",  "description": "...", "tag": "Half",  "iconClass": "epi-icon__layout--half" }
]
```

### 6. Template Resolution - `DisplayOptionsModelTemplateTagProvider`

**File:** `EPiServer.Cms.AspNetCore.Templating/Web/Templating/Internal/DisplayOptionsModelTemplateTagProvider.cs`

This plugs into the template resolution pipeline. When the `ContentAreaRenderer` resolves which partial view to use, this provider contributes the display option's `Tag`:

```csharp
internal class DisplayOptionsModelTemplateTagProvider : IModelTemplateTagProvider
{
    public int Order => 100;

    public IEnumerable<string> Resolve(ModelExplorer modelExplorer, ViewContext viewContext)
    {
        if (modelExplorer.Model is ContentAreaItem contentAreaItem)
        {
            var displayOption = _contentAreaLoader.LoadDisplayOption(contentAreaItem);
            if (displayOption != null)
                return new[] { displayOption.Tag };
        }
        return Enumerable.Empty<string>();
    }
}
```

The `Tag` value (e.g., `"Full"`, `"Half"`) is used by the `TemplateResolver` to find a matching partial view or view component.

### 7. Rendering - `ContentAreaRenderer`

**File:** `EPiServer.Cms.AspNetCore.HtmlHelpers/Web/Mvc/Html/ContentAreaRenderer.cs`

The renderer orchestrates the full rendering flow for each `ContentAreaItem`:

```csharp
protected virtual void RenderContentAreaItem(IHtmlHelper htmlHelper, ContentAreaItem contentAreaItem,
    string templateTag, string htmlTag, string cssClass)
{
    // 1. Build render settings dictionary
    var renderSettings = new Dictionary<string, object>
    {
        [RenderSettings.ChildrenCustomTagName] = htmlTag,
        [RenderSettings.ChildrenCssClass] = cssClass,
        [RenderSettings.Tag] = templateTag
    };

    // 2. Merge content area item's render settings (includes display option)
    foreach (var renderSetting in contentAreaItem.RenderSettings)
        renderSettings[renderSetting.Key] = renderSetting.Value;

    htmlHelper.ViewBag.RenderSettings = renderSettings;

    // 3. Load the block content
    var content = _contentAreaLoader.LoadContent(contentAreaItem);

    // 4. Resolve template using tags (display option tag participates)
    var tags = _modelTagResolver.Resolve(...);
    var templateModel = ResolveContentTemplate(htmlHelper, content, tags);

    // 5. Wrap in HTML element with attributes (including data-epi-content-display-option)
    var tagBuilder = new TagBuilder(htmlTag);
    AddNonEmptyCssClass(tagBuilder, cssClass);
    tagBuilder.MergeAttributes(_attributeAssembler.GetAttributes(contentAreaItem, IsInEditMode(), templateModel != null));

    htmlHelper.ViewContext.Writer.Write(tagBuilder.RenderStartTag());
    htmlHelper.RenderContentData(content, true, templateModel, _contentRenderer);
    htmlHelper.ViewContext.Writer.Write(tagBuilder.RenderEndTag());
}
```

The critical method that reads the display option:

```csharp
protected virtual string GetContentAreaItemTemplateTag(IHtmlHelper htmlHelper, ContentAreaItem contentAreaItem)
{
    var displayOption = _contentAreaLoader.LoadDisplayOption(contentAreaItem);
    if (displayOption != null)
        return displayOption.Tag;
    return GetContentAreaTemplateTag(htmlHelper);  // fallback to content area-level tag
}
```

### 8. Edit-Mode HTML Attributes - `DefaultContentAreaItemAttributeAssembler`

**File:** `EPiServer.Cms.AspNetCore.Templating/Web/Internal/DefaultContentAreaItemAttributeAssembler.cs`

In edit mode, all `RenderSettings` (including the display option) are emitted as HTML `data-*` attributes on the wrapping element:

```csharp
if (contentAreaItem.RenderSettings != null)
{
    foreach (var attribute in contentAreaItem.RenderSettings.Where(setting => setting.Value != null))
    {
        attributes[attribute.Key] = attribute.Value.ToString();
    }
}
```

This means the Dojo UI can read `data-epi-content-display-option="full"` from the DOM element and reflect it in the editor overlay.

---

## Dojo Client-Side Implementation

### 1. Store Registration - `CMSModule.js`

**File:** `epi-cms/CMSModule.js`

The display options REST store is registered during module initialization:

```javascript
registry.create("epi.cms.displayoptions", this._getRestPath("displayoptions"));
```

This creates a Dojo `JsonRest` store that fetches from `/EPiServer/cms/Stores/displayoptions/`.

### 2. View Model - `ContentBlockViewModel.js`

**File:** `epi-cms/contentediting/viewmodel/ContentBlockViewModel.js`

Each block in a content area is represented by a `ContentBlockViewModel`. It stores the display option selection in the `attributes` dictionary using a well-known key:

```javascript
// Settings object with the attribute name constant
settings: {
    displayOptionsAttributeName: "data-epi-content-display-option",
    contentGroupAttributeName: "data-contentgroup"
},

// Getter/setter for the displayOption property
_displayOptionSetter: function (option) {
    this.attributes[this.settings.displayOptionsAttributeName] = option;
},

_displayOptionGetter: function () {
    return this.attributes[this.settings.displayOptionsAttributeName];
}
```

When the model is serialized (e.g., on save), the `attributes` object is included in the payload, and the `data-epi-content-display-option` key becomes part of `ContentAreaItem.RenderSettings` on the server.

### 3. Command - `SelectDisplayOption.js`

**File:** `epi-cms/contentediting/command/SelectDisplayOption.js`

This is the context menu command that shows the display option popup. It extends `_ContentAreaCommand`:

```javascript
return declare([_ContentAreaCommand], {
    label: resources.label,         // "Display As: {0}"
    category: "popup",              // Renders as a sub-menu

    constructor: function () {
        // Create the popup widget
        this.popup = new DisplayOptionSelector();
    },

    postscript: function () {
        // Fetch available display options from the REST store
        if (!this.store) {
            var registry = dependency.resolve("epi.storeregistry");
            this.store = registry.get("epi.cms.displayoptions");
        }

        when(this.store.get(), lang.hitch(this, function (options) {
            this._setCommandAvailable(options);
            this.popup.set("displayOptions", options);
        }));
    },

    _onModelChange: function () {
        // Show command only when display options exist and model is a ContentBlockViewModel
        var options = this.popup.displayOptions;
        var isAvailable = options && options.length > 0
            && (this.model instanceof ContentBlockViewModel);

        this._setCommandAvailable(options);

        // Update label to show current selection
        var selectedOption = this.model.get("displayOption");
        if (!selectedOption) {
            this.set("label", this._labelAutomatic);  // "Display As: Automatic"
        } else {
            this._setLabel(selectedOption);  // "Display As: Full Width"
        }
    },

    _onModelValueChange: function () {
        // Can execute if the block has content and isn't read-only
        this.set("canExecute",
            !!this.model && (this.model.contentLink || this.model.inlineBlockData)
            && !this.model.get("readOnly"));
    }
});
```

### 4. Popup Widget - `DisplayOptionSelector.js`

**File:** `epi-cms/widget/DisplayOptionSelector.js`

A menu widget that shows radio buttons for each display option:

```javascript
return declare([SelectorMenuBase, DestroyableByKey], {
    headingText: resources.title,  // "Display options"

    postCreate: function () {
        // Add "Automatic" radio button (clears the selection)
        this._rdAutomatic = new RadioMenuItem({ label: resources.automatic, value: "" });
        this.addChild(this._rdAutomatic);
        this._rdAutomatic.on("change", lang.hitch(this, this._restoreDefault));
    },

    _restoreDefault: function () {
        // Set displayOption to null (removes from RenderSettings)
        this.model.modify(function () {
            this.model.set("displayOption", null);
        }, this);
    },

    _setup: function () {
        // Build radio buttons for each option
        array.forEach(this.displayOptions, function (displayOption) {
            var item = new RadioMenuItem({
                label: displayOption.name,
                iconClass: displayOption.iconClass,
                checked: selectedDisplayOption === displayOption.id,
                title: displayOption.description
            });

            // When a radio changes, update the model
            item.watch("checked", function (property, oldValue, newValue) {
                if (!newValue) return;
                this.model.modify(function () {
                    this.model.set("displayOption", displayOption.id);
                }, this);
            });

            this.addChild(item);
        }, this);
    }
});
```

### 5. Wiring Into Content Area Editors

The `SelectDisplayOption` command is added to the command list in two places:

**Forms editing mode** (`ContentAreaEditor.js`):

```javascript
this.commands = [
    new ConnectInlineContentCommand(...),
    new DisconnectContentCommand(...),
    new EditCommand({ category: null }),
    this.contentAreaItemBlockEdit,
    this.blockInlineEdit,
    this._commandSpliter,
    new SelectDisplayOption(),     // ← HERE
    this.movePrevious,
    this.moveNext,
    new RemoveCommand(),
    new BlockConvertCommand()
];
```

**On-page editing mode** (`ContentAreaCommands.js`):

```javascript
this.commands = [
    new Edit({ category: null }),
    this.contentAreaItemBlockEdit,
    this.blockInlineEdit,
    this._commandSpliter,
    new SelectDisplayOption(),     // ← HERE
    this.moveVisibleToPrevious,
    this.moveVisibleToNext,
    new Remove()
];
```

---

## Data Flow Summary

### Editor Selects a Display Option

1. Editor right-clicks block → Context menu → "Display As: Automatic" sub-menu
2. SelectDisplayOption command shows DisplayOptionSelector popup
3. Editor picks "Full Width"
4. DisplayOptionSelector calls: model.set("displayOption", "full")
5. ContentBlockViewModel._displayOptionSetter stores:

   ```cs
   attributes["data-epi-content-display-option"] = "full"
   ```

6. On save, model.serialize() includes the attributes dictionary
7. Server persists it to ContentAreaItem.RenderSettings["data-epi-content-display-option"] = "full"

### Page Renders with Display Option

1. ContentAreaRenderer.RenderContentAreaItem() called for each item
2. GetContentAreaItemTemplateTag() → LoadDisplayOption(item) → reads "data-epi-content-display-option"
3. Looks up DisplayOption by id "full" → gets Tag "Full"
4. ResolveContentTemplate(content, ["Full"]) → finds partial view tagged "Full"
5. Renders the block inside `<div data-epi-content-display-option="full" class="...">`

## Building a Custom Selector (e.g. Theme, Margin)

The Display Options pattern can be replicated to build any per-content-area-item selector. The key insight is that **`ContentAreaItem.RenderSettings` is a general-purpose dictionary** - you can store any `data-*` prefixed key-value pair, and it will be persisted and output as an HTML attribute.

### Step-by-Step C# Implementation

#### 1. Define Your Attribute Name Constant

```csharp
public static class CustomRenderSettings
{
    /// <summary>
    /// The key used in RenderSettings to store the theme selection.
    /// Must be prefixed with "data-" to be persisted.
    /// </summary>
    public const string ThemeAttributeName = "data-custom-theme";

    /// <summary>
    /// The key used in RenderSettings to store the margin selection.
    /// </summary>
    public const string MarginAttributeName = "data-custom-margin";
}
```

#### 2. Create Your Option Model

```csharp
public class ThemeOption
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string CssClass { get; set; }
    public string IconClass { get; set; }
}
```

#### 3. Create a Registry

```csharp
public class ThemeOptions : IEnumerable<ThemeOption>
{
    private readonly List<ThemeOption> _options = new();

    public ThemeOptions Add(ThemeOption option) { _options.Add(option); return this; }
    public ThemeOption Get(string id) => _options.FirstOrDefault(o => o.Id == id);
    public IEnumerator<ThemeOption> GetEnumerator() => _options.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

#### 4. Register Options at Startup

```csharp
// In Startup.cs or an initialization module
services.AddSingleton<ThemeOptions>(sp =>
{
    var options = new ThemeOptions();
    options.Add(new ThemeOption { Id = "light", Name = "Light Theme", CssClass = "theme-light", IconClass = "epi-icon-light" });
    options.Add(new ThemeOption { Id = "dark",  Name = "Dark Theme",  CssClass = "theme-dark",  IconClass = "epi-icon-dark" });
    return options;
});
```

#### 5. Create a REST Store

```csharp
[RestStore("customthemeoptions")]
public class ThemeOptionsStore : RestControllerBase
{
    private readonly ThemeOptions _themeOptions;

    public ThemeOptionsStore(ThemeOptions themeOptions) => _themeOptions = themeOptions;

    public ActionResult Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Rest(_themeOptions.ToList());

        var option = _themeOptions.Get(id);
        return option != null ? Rest(option) : NotFound();
    }
}
```

#### 6. Read During Rendering

Override `ContentAreaRenderer` or read from `ContentAreaItem.RenderSettings` in your views:

```csharp
// In a custom ContentAreaRenderer
protected override string GetContentAreaItemCssClass(IHtmlHelper htmlHelper, ContentAreaItem contentAreaItem)
{
    var baseCss = base.GetContentAreaItemCssClass(htmlHelper, contentAreaItem);

    if (contentAreaItem.RenderSettings.TryGetValue(CustomRenderSettings.ThemeAttributeName, out var themeId))
    {
        var themeOptions = _serviceProvider.GetRequiredService<ThemeOptions>();
        var theme = themeOptions.Get(themeId);
        if (theme != null)
        {
            return string.IsNullOrEmpty(baseCss)
                ? theme.CssClass
                : $"{baseCss} {theme.CssClass}";
        }
    }

    return baseCss;
}
```

Or read it in Razor views from ViewBag:

```razor
@{
    var renderSettings = ViewBag.RenderSettings as IDictionary<string, object>;
    var themeId = renderSettings?["data-custom-theme"]?.ToString();
}
<div class="block-wrapper @(themeId == "dark" ? "theme-dark" : "theme-light")">
    @Html.PropertyFor(m => m.MainBody)
</div>
```

### Step-by-Step Dojo Implementation

#### 1. Register the Store in a Module Initializer

Create a module initializer or extend the existing one:

```javascript
// custom-module/CustomModuleInitializer.js
define([
    "epi/dependency"
], function (dependency) {
    return {
        initialize: function () {
            var registry = dependency.resolve("epi.storeregistry");
            // Register the custom store - maps to the [RestStore("customthemeoptions")] controller
            registry.create("custom.themeoptions", this._getRestPath("customthemeoptions"));
        }
    };
});
```

#### 2. Create the Selector Widget

Model your widget after `DisplayOptionSelector.js`:

```javascript
// custom-module/widget/ThemeSelector.js
define([
    "dojo/_base/array",
    "dojo/_base/declare",
    "dojo/_base/lang",
    "epi/shell/DestroyableByKey",
    "epi-cms/widget/SelectorMenuBase",
    "epi/shell/widget/RadioMenuItem"
], function (array, declare, lang, DestroyableByKey, SelectorMenuBase, RadioMenuItem) {

    return declare([SelectorMenuBase, DestroyableByKey], {
        headingText: "Theme",

        // The attribute name in ContentBlockViewModel.attributes
        // Must match the C# constant: "data-custom-theme"
        attributeName: "data-custom-theme",

        model: null,
        themeOptions: null,
        _rdAutomatic: null,

        postCreate: function () {
            this.inherited(arguments);

            // "Default" option that clears the selection
            this.own(this._rdAutomatic = new RadioMenuItem({ label: "Default", value: "" }));
            this.addChild(this._rdAutomatic);
            this.own(this._rdAutomatic.on("change", lang.hitch(this, function () {
                this.model.modify(function () {
                    this.model.attributes[this.attributeName] = null;
                }, this);
            })));
        },

        _setModelAttr: function (model) {
            this._set("model", model);
            this._setup();
        },

        _setThemeOptionsAttr: function (options) {
            this._set("themeOptions", options);
            this._setup();
        },

        _setup: function () {
            if (!this.model || !this.themeOptions) {
                return;
            }

            this._removeMenuItems();

            var currentValue = this.model.attributes[this.attributeName];

            array.forEach(this.themeOptions, function (option) {
                var item = new RadioMenuItem({
                    label: option.name,
                    iconClass: option.iconClass,
                    checked: currentValue === option.id,
                    title: option.description
                });

                this.ownByKey("items", item.watch("checked", lang.hitch(this, function (prop, oldVal, newVal) {
                    if (!newVal) return;
                    this.model.modify(function () {
                        this.model.attributes[this.attributeName] = option.id;
                    }, this);
                })));

                this.addChild(item);
            }, this);

            this._rdAutomatic.set("checked", !currentValue);
        },

        _removeMenuItems: function () {
            var items = this.getChildren();
            this.destroyByKey("items");
            items.forEach(function (item) {
                if (item === this._rdAutomatic) return;
                this.removeChild(item);
                item.destroy();
            }, this);
        }
    });
});
```

#### 3. Create the Command

Model your command after `SelectDisplayOption.js`:

```javascript
// custom-module/command/SelectTheme.js
define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/when",
    "epi/dependency",
    "epi-cms/contentediting/command/_ContentAreaCommand",
    "epi-cms/contentediting/viewmodel/ContentBlockViewModel",
    "custom-module/widget/ThemeSelector"
], function (declare, lang, when, dependency, _ContentAreaCommand, ContentBlockViewModel, ThemeSelector) {

    return declare([_ContentAreaCommand], {
        label: "Theme: Default",
        category: "popup",    // Shows as a sub-menu in the context menu

        attributeName: "data-custom-theme",

        constructor: function () {
            this.popup = new ThemeSelector();
        },

        postscript: function () {
            this.inherited(arguments);

            if (!this.store) {
                var registry = dependency.resolve("epi.storeregistry");
                this.store = registry.get("custom.themeoptions");
            }

            when(this.store.get(), lang.hitch(this, function (options) {
                this.set("isAvailable", options && options.length > 0);
                this.popup.set("themeOptions", options);
            }));
        },

        destroy: function () {
            this.inherited(arguments);
            this.popup && this.popup.destroyRecursive();
        },

        _onModelChange: function () {
            if (!this.model) {
                this.set("isAvailable", false);
                return;
            }

            this.inherited(arguments);

            var options = this.popup.themeOptions;
            var isAvailable = options && options.length > 0
                && (this.model instanceof ContentBlockViewModel);

            this.set("isAvailable", isAvailable);

            if (!isAvailable) return;

            this.popup.set("model", this.model);

            var selectedValue = this.model.attributes[this.attributeName];
            if (!selectedValue) {
                this.set("label", "Theme: Default");
            } else {
                this._setLabel(selectedValue);
            }

            this._watch(this.attributeName, function (prop, oldVal, newVal) {
                if (!newVal) {
                    this.set("label", "Theme: Default");
                } else {
                    this._setLabel(newVal);
                }
            }, this);
        },

        _setLabel: function (themeId) {
            when(this.store.get(themeId), lang.hitch(this, function (option) {
                this.set("label", "Theme: " + option.name);
            }), lang.hitch(this, function () {
                this.set("label", "Theme: Default");
            }));
        },

        _onModelValueChange: function () {
            this.set("canExecute",
                !!this.model
                && (this.model.contentLink || this.model.inlineBlockData)
                && !this.model.get("readOnly"));
        }
    });
});
```

#### 4. Wire the Command Into the Content Area Editor

You need to extend or decorate the content area editor's command list. The standard approach is to use a **command provider** or extend `ContentAreaEditor`:

```javascript
// In your module initializer or editor descriptor
define([
    "custom-module/command/SelectTheme"
], function (SelectTheme) {
    // Add the new command to the content area's command list
    // (The exact wiring depends on how your site is structured -
    //  you may need to extend ContentAreaEditor or use a command provider)
});
```

### Reading Custom Settings During Rendering

All `data-*` prefixed keys in `RenderSettings` are:

1. **Persisted** to the database with the content.
2. **Available in edit mode** as HTML attributes on the wrapping `<div>`.
3. **Available at render time** via `ContentAreaItem.RenderSettings` and `ViewBag.RenderSettings`.

```csharp
// Reading in a view or tag helper
var themeId = contentAreaItem.RenderSettings
    .TryGetValue("data-custom-theme", out var value) ? value : null;
```

---

## Key Conventions and Constraints

| Convention                     | Details                                                                                                                                                                                                                                                                                             |
| ------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`data-` prefix required**    | Only `RenderSettings` keys prefixed with `"data-"` are persisted to the database. Other keys are transient.                                                                                                                                                                                         |
| **Attribute name constants**   | Use a static class to define your attribute name constants (like `ContentFragment.ContentDisplayOptionAttributeName`).                                                                                                                                                                              |
| **`category: "popup"`**        | Commands with this category render as a sub-menu in the content area context menu.                                                                                                                                                                                                                  |
| **`_ContentAreaCommand` base** | All content area item commands should extend this base class, which handles model watching and lifecycle.                                                                                                                                                                                           |
| **`ContentBlockViewModel`**    | The `attributes` dictionary on this view model maps directly to `ContentAreaItem.RenderSettings`.                                                                                                                                                                                                   |
| **`model.modify(fn)`**         | Always wrap model mutations in `model.modify()` to ensure proper change tracking and save integration.                                                                                                                                                                                              |
| **`IReadOnly` pattern**        | Options are made read-only after initialization. If you need to modify at runtime, use `CreateWritableClone()`.                                                                                                                                                                                     |
| **Localization**               | `Name` and `Description` support localization resource keys. The `DisplayOptionModel` resolves them via `LocalizationService`.                                                                                                                                                                      |
| **Template tags**              | Display Options use the `Tag` property to influence template resolution. Your custom selectors can apply CSS classes, data attributes, or other rendering hints instead.                                                                                                                            |
| **`[RestStore("name")]`**      | The attribute name must match what you pass to `registry.create("storename", path)` in the Dojo module.                                                                                                                                                                                             |
| **REST store action naming**   | REST store controllers use conventional routing with `action = "Get"` for GET requests. Your controller must have a single `Get(string id)` method — do not use `[HttpGet]` attributes or different action names like `GetAll()`, as these break the conventional routing and result in 404 errors. |
