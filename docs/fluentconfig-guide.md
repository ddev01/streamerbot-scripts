# FluentConfig authoring guide (for agents)

Concise usage notes for writing Streamer.bot settings UIs with FluentConfig.
Signatures: see [fluentconfig-api.md](fluentconfig-api.md).
Full upstream docs/examples: `F:\Dev\SB-FluentConfig\docs\` and `F:\Dev\SB-FluentConfig\examples\`.
Layout deep-dive (1:1 Tailwind Size/Grid/Row): `F:\Dev\SB-FluentConfig\docs\guides\LAYOUT.md`.
Runtime values / dialogs: `F:\Dev\SB-FluentConfig\docs\guides\DIALOGS_AND_RUNTIME_VALUES.md`.

## Critical Streamer.bot setup

- Use **Execute C# Method** with **Run on UI thread** enabled (required for WebView2 host).
- Common pattern: disabled **Execute C# Code** (holds source) + **Execute C# Method** calling `Execute()`.
- Assembly refs: PresentationFramework, PresentationCore, WindowsBase, and `FluentConfig.dll` from Streamer.bot `dlls/`.
- Do **not** copy WebView2 DLLs into `dlls/` (Streamer.bot already loads them).

## Minimal menu

Prefer `Fc.Open` (focus if already open, otherwise create/build/show):

```csharp
using FluentConfig;

public class CPHInline
{
    private const string Title = "My Extension";
    private const string Version = "1.0";

    public bool Execute()
    {
        Fc.Open(CPH, Title, Version, ui => ui
            .Section("Settings", "settings", s => s
                .Intro("Configure the extension.")
                .Toggle("Enabled", "enabled").Default(true)
                .Textbox("Name", "name")));
        return true;
    }
}
```

Equivalent granular form: `Fc.AlreadyOpened` → `FluentConfigUi.Create` → `.Section(...)` → `.Show()`.
`FluentConfigUi.ShowOrFocus(...)` is the same as `Fc.Open`.

Local `SbFormat` / Ctrl+S keeps FluentConfig chains in this Laravel layout. Streamer.bot’s Format Document still flattens them — re-run local format afterward if needed.

## Controls

On `SectionBuilder` / `PanelBuilder`: Toggle, Textbox, Slider, Dropdown, Combobox, NumberInput, IntegerInput,
DurationInput, Filepath, ColorPicker, DynamicTextboxes, PillInput, Button, ConnectionStatus,
Intro, Title, Separator, WithVisibility / WithVisibilityWhenOff / WithVisibilityWhenNot, WithRepeatableRows,
Grid, Row, RepeatFor.

Chain options from `IControlOptions`: `.Hint()`, `.Default(...)`, `.Range()`, `.Password()`,
`.Options(...)`, `.Searchable()`, `.AllowCustom()`, `.Multiple()`, `.OnClick(...)`, `.ItemTemplate(...)`,
`.Size(...)` (Tailwind classes), etc.
There is **no** `.Span()` / `.Width()` — use `.Size("col-span-2")` / `.Size("w-1/2")`.

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

Value-equals (dropdown / any saved value — not a numeric comparator). Invert is a **named method**, not a trailing bool:

```csharp
.Dropdown("Mode", "default_mode").Options(new[] { "discount", "random", "free" })
.NumberInput("Multiplier", "multiplier").ShowWhen("default_mode", "discount")
.WithVisibility("default_mode", "random", inner => inner
    .Grid("grid-cols-2 items-center", g => g
        .NumberInput("Random min", "random_min")
        .NumberInput("Random max", "random_max")))
.ShowWhenNot("default_mode", "free")
.WithVisibilityWhenNot("default_mode", "free", inner => inner.Textbox("Paid", "paid_note"))
```

Do **not** nest `Comparator.GreaterOrEqual` + `LessOrEqual` to fake equality. There is no `Comparator.Equal`.
Value-equals / comparator groups are visually **flat** (no extra left rail). Toggle groups keep the indent rail.
Rare escape hatch: last arg `VisibilityChrome.Indented` (not a `bool`).

Numeric comparators (live, no remount):

```csharp
.IntegerInput("Bonus", "bonus")
    .ShowWhen("max_count", Comparator.GreaterOrEqual, 5)

.WithVisibility("level", Comparator.GreaterThan, 0, inner => inner
    .Textbox("Detail", "detail"))
```

`Comparator`: `GreaterOrEqual`, `LessOrEqual`, `GreaterThan`, `LessThan` (wire: `gte`/`lte`/`gt`/`lt`).

Upstream: `F:\Dev\SB-FluentConfig\docs\guides\VISIBILITY.md`, tutorial `04_ConditionalVisibility.cs`.

## Dropdown / combobox

`.Searchable()` filters as the user types. `.AllowCustom()` commits values not in the list (kept across Refresh).
`.Multiple()` stores `string[]` — incompatible with `.WithPairValue()`. `.Combobox(label, key)` is a dropdown with searchable on.
`.Default(string)` works on single dropdowns; `.Default(string[])` is for `.Multiple()` (otherwise seeds `[]`).

```csharp
.Combobox("Auto-end timer", "timer_id")
    .AllowCustom()
    .RefreshPairs(() => new[] { ("timer-a", "Timer A"), ("timer-b", "Timer B") })

.Dropdown("Include groups", "include_groups")
    .Searchable()
    .Multiple()
    .AllowCustom()
    .Refresh(() => Fc.TwitchRewardGroups(CPH))
```

Refresh callbacks are **author-supplied**. There is **no** `Fc.ListTimers` / `CPH.GetTimers()` — Streamer.bot only has Enable/Disable/interval/state. Pass your own list into `.Refresh` / `.RefreshPairs`.

`Fc.TwitchRewardGroups(CPH)` returns distinct trimmed group names from `CPH.TwitchGetRewards()` (never null).

## DurationInput

`.WithPermanentOption(true)` shows a Permanent checkbox. When checked, the amount and unit controls are **hidden** (not merely disabled); the saved value is `"permanent"`. Unchecking restores a real duration (`amount || 30` + last unit).

## Dynamic field count (RepeatFor)

When an integer setting drives how many related fields appear, use `RepeatFor` instead of building a fixed loop from saved settings. Fields show/hide live as the driver changes — no reopen.

```csharp
.Section("Settings", "Settings", s => s
    .IntegerInput("Max places", "max_count")
        .Range(1, 10)
        .Default(3)
        .Size("w-fit")
)
.Section("Points", "Points", s => s
    .RepeatFor("max_count", (row, i) => row
        .IntegerInput($"Place #{i} points", $"points_{i}")
            .Range(0, 100000)
            .Default(i == 1 ? 1000 : 0)
            .Size("w-fit")
    )
)
```

- Max defaults from the driver's `.Range(min, max)` (declare the driver earlier, including in a prior section). Pass `max:` to override.
- Indices at/below the driver's min are always visible; higher indices gate with `gte`.
- Values are kept when the count shrinks (visibility only).
- Example: `F:\Dev\SB-FluentConfig\examples\tutorial\05_LayoutAndRepeatFor.cs`

## Layout: Grid, Row, Size

**1:1 Tailwind class names** in compound specs. Host resolves them to CSS on the wire (Svelte never sees raw class strings from JSON).

```csharp
.Grid("grid-cols-2 gap-3 items-center", g => g
    .Toggle("Exclude broadcaster", "exclude_broadcaster").Default(true)
    .Toggle("Exclude bots", "exclude_bot").Default(true)
    .Textbox("Prefix", "announce_prefix")
        .Default("Winners:")
        .Size("col-span-2")
)

.Row("gap-2 items-center", r => r
    .Textbox("Name", "name").Size("grow")
    .Button("Go").Text("Run").Size("shrink-0")
)

.IntegerInput("Count", "max_count")
    .Range(1, 10)
    .Size("w-fit min-w-20")  // hug input; label stays full width
```

| Surface | Classes |
|---------|---------|
| `Grid` / `Row` | `grid-cols-N` (Grid only), `gap-N`, `items-start\|center\|end\|stretch\|baseline` |
| `.Size(...)` | `w-fit`, `w-full`, `w-1/2`, `w-[120px]`, `min-w-*`, `max-w-*`, `col-span-N` (Grid only), `grow`/`grow-0`/`shrink`/`shrink-0` (Row only) |

Bare aliases like `fit` or `1/2` are rejected — always use `w-fit` / `w-1/2`. Full tables: `F:\Dev\SB-FluentConfig\docs\guides\LAYOUT.md`.

## Reading / writing settings at runtime

Persisted settings key: **`{slug}_settings`** (title slugified), e.g. `"My Extension"` → `my_extension_settings`.
Use `Fc.SettingsKeyFor(title)` / `Fc.SlugFor(title)` — do **not** use a `FluentConfig_Settings_*` prefix.

| Context | API |
|---------|-----|
| After save / outside UI (typed) | `Fc.LoadSettings<T>(CPH, title)` |
| Single field / nested path | `Fc.GetSetting<T>(CPH, title, jsonKey, default)` |
| Write one field (RMW) | `Fc.SetSetting(CPH, title, jsonKey, value)` |
| Mutate several keys then save | `Fc.SaveSettings(CPH, title, o => { ... })` |
| Never opened/saved the menu? | `Fc.HasSavedSettings(CPH, title)` |
| Runtime state (not the menu) | `Fc.LoadData<T>` / `Fc.SaveData` / `GetData` / `SetData` → `{slug}_data` |
| Event args + chat templates | `Fc.CaptureEvent(CPH)` / `Fc.ApplyTemplate(...)` |
| Structured logs | `Fc.Logger(CPH, title, version)` → `Init` / `Info` / `Warn` / `Error` / `Failed` |
| Reward group names | `Fc.TwitchRewardGroups(CPH)` |
| Button `OnClick` | `UiContext.Pending<T>(saveKey)` |
| Pill add/remove | `CallbackContext` helpers |

```csharp
if (!Fc.HasSavedSettings(CPH, Title))
{
    Fc.Logger(CPH, Title, Version).Info("Open settings and Save first.");
    return true;
}

var settings = Fc.LoadSettings<MySettings>(CPH, Title);
Fc.SetSetting(CPH, Title, "access_token", "");

var ev = Fc.CaptureEvent(CPH);
string chat = Fc.ApplyTemplate("Hi %user%!", ev); // also supports {user}

var state = Fc.LoadData<MyState>(CPH, Title); // → my_extension_data
state.Count++;
Fc.SaveData(CPH, Title, state);
```

Examples: `F:\Dev\SB-FluentConfig\examples\tutorial\10_ReadingSavedSettings.cs`,
`11_RuntimeHelpers.cs`. Guide: `DIALOGS_AND_RUNTIME_VALUES.md`.

`.LogExistingSettings()` on the menu builder redacts secrets (`token`, `password`, `secret`, …) before logging.

## Updates

- DLL install/update: copy-paste DllCheck from `F:\Dev\SB-FluentConfig\examples\deployment\01_DllCheck.cs` (no FluentConfig reference).
- Extension notify modal: `.WithExtensionUpdateNotice("owner/repo", "1.0.0")` on the UI builder — see `examples/deployment/02_ExtensionUpdateNotice.cs`.
- Prefer examples in `F:\Dev\SB-FluentConfig\examples\` over inventing new patterns.
