# FluentConfig authoring guide (for agents)

Concise usage notes for writing Streamer.bot settings UIs with FluentConfig.
Signatures: see [fluentconfig-api.md](fluentconfig-api.md).
Full upstream docs/examples: `F:\Dev\SB-FluentConfig\docs\` and `F:\Dev\SB-FluentConfig\examples\`.

## Critical Streamer.bot setup

- Use **Execute C# Method** with **Run on UI thread** enabled (required for WebView2 host).
- Common pattern: disabled **Execute C# Code** (holds source) + **Execute C# Method** calling `Execute()`.
- Assembly refs: PresentationFramework, PresentationCore, WindowsBase, and `FluentConfig.dll` from Streamer.bot `dlls/`.
- Do **not** copy WebView2 DLLs into `dlls/` (Streamer.bot already loads them).

## Minimal menu

```csharp
using FluentConfig;

public class CPHInline
{
    public bool Execute()
    {
        if (FluentConfig.FluentConfig.AlreadyOpened("My Extension", "1.0"))
            return true;

        FluentConfigUi.Create(CPH, "My Extension", "1.0")
            .Section("Settings", "Settings", s => s
                .Intro("Configure the extension.")
                .Toggle("Enabled", "enabled").Default(true)
                .Textbox("Name", "name"))
            .Show();

        return true;
    }
}
```

Or collapse open/focus: `FluentConfigUi.ShowOrFocus(CPH, title, version, ui => ui.Section(...));`

## Controls

On `SectionBuilder` / `PanelBuilder`: Toggle, Textbox, Slider, Dropdown, NumberInput, IntegerInput,
DurationInput, Filepath, ColorPicker, DynamicTextboxes, PillInput, Button, ConnectionStatus,
Intro, Title, Separator, WithVisibility / WithVisibilityWhenOff, WithRepeatableRows.

Chain options from `IControlOptions` on the pending control: `.Hint()`, `.Default(...)`, `.Range()`,
`.Password()`, `.Options(...)`, `.OnClick(...)`, `.ItemTemplate(...)`, etc.

## Pills

```csharp
.PillInput("Items", "items")
    .ItemTemplate(pb => pb
        .Title("Item: {name}")
        .Toggle("Enabled", "{name}_enabled").Default(true))
    .OnPillRemoved((item, ctx) => { /* cleanup keys */ })
```

Do **not** use WPF `Panel` / `StackPanel` in pill callbacks — schema sub-builders only.

## Visibility

```csharp
.Toggle("Show extras", "show_extra")
.WithVisibility("show_extra", inner => inner.Textbox("Extra", "extra_value"))
.WithVisibilityWhenOff("premium_mode", inner => inner.Textbox("Free", "free_tier"))
```

## Reading settings at runtime

| Context | API |
|---------|-----|
| After save / outside UI | `CPH.GetGlobalVar<string>("FluentConfig_Settings_{title}", true)` then parse JSON |
| Button `OnClick` | `UiContext.Pending<T>(saveKey)` |
| Pill add/remove | `CallbackContext` helpers |

Persisted global key pattern: `FluentConfig_Settings_{title}`.

## Updates

- DLL install/update: copy-paste `DllCheckExample` (no FluentConfig reference) — see examples/updater.
- Extension notify modal: `.WithExtensionUpdateNotice("owner/repo", "1.0.0")` on the UI builder.
- Prefer examples in `F:\Dev\SB-FluentConfig\examples\` over inventing new patterns.
