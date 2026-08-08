# FluentConfig API Reference (generated)

Source: `F:\Stream\Streamer.bot-x64-1.0.7\dlls\FluentConfig.dll`
Generated: 2026-08-08
Author source/docs: `F:\Dev\SB-FluentConfig`

Authoring surface only. Control factories are on `SectionBuilder` / `PanelBuilder`; option methods chain via `IControlOptions`.
Usage patterns: [fluentconfig-guide.md](fluentconfig-guide.md).

## class FluentConfig.FluentConfigUi

```csharp
FluentConfigUi Icon(string iconPath)
FluentConfigUi LogExistingSettings()
FluentConfigUi RepoUrl(string repoUrl)
FluentConfigUi Section(string title, string tabId, Action<SectionBuilder> build)
FluentConfigUi WithExtensionUpdateNotice(string repo, string currentVersion, string tagPrefix = ..., string minStreamerBot = ..., string minFluentConfig = ..., string updateGuideUrl = ...)
static FluentConfigUi Create(IInlineInvokeProxy cph, string title, string version, bool plainWindow = ...)
static void ShowOrFocus(IInlineInvokeProxy cph, string title, string version, Action<FluentConfigUi> build, string iconPath = ...)
void Show()
```

## class FluentConfig.FluentConfig

```csharp
static bool AlreadyOpened(string title = ..., string version = ...)
static string GetVersion()
static void SetLogCallback(Action<string> callback)
static void SetWindowClosedCallback(Action<double, double, double, double> callback)
static void SetWindowClosedCallback(Action<double, double> callback)
```

## class FluentConfig.SectionBuilder

```csharp
SectionBuilder ConnectionStatus(string label, string initialStatus, string buttonText = ..., Action<UiContext> onClick = ...)
SectionBuilder Intro(string text)
SectionBuilder Separator()
SectionBuilder Title(string text)
SectionBuilder WithRepeatableRows(string saveKey, Action<PanelBuilder> buildRow)
SectionBuilder WithVisibility(string toggleKey, Action<PanelBuilder> build, bool inverted = ...)
SectionBuilder WithVisibilityWhenOff(string toggleKey, Action<PanelBuilder> build)
SectionFluentWrapper Button(string label)
SectionFluentWrapper ColorPicker(string label, string key)
SectionFluentWrapper Dropdown(string label, string key)
SectionFluentWrapper DurationInput(string label, string key)
SectionFluentWrapper DynamicTextboxes(string label, string key)
SectionFluentWrapper Filepath(string label, string key)
SectionFluentWrapper IntegerInput(string label, string key)
SectionFluentWrapper NumberInput(string label, string key)
SectionFluentWrapper PillInput(string label, string key)
SectionFluentWrapper Slider(string label, string key)
SectionFluentWrapper Textbox(string label, string key)
SectionFluentWrapper Toggle(string label, string key)
```

## class FluentConfig.PanelBuilder

```csharp
PanelBuilder ConnectionStatus(string label, string initialStatus, string buttonText = ..., Action<UiContext> onClick = ...)
PanelBuilder Intro(string text)
PanelBuilder Separator()
PanelBuilder Title(string text)
PanelBuilder WithRepeatableRows(string saveKey, Action<PanelBuilder> buildRow)
PanelBuilder WithVisibility(string toggleKey, Action<PanelBuilder> build, bool inverted = ...)
PanelBuilder WithVisibilityWhenOff(string toggleKey, Action<PanelBuilder> build)
PanelFluentWrapper Button(string label)
PanelFluentWrapper ColorPicker(string label, string key)
PanelFluentWrapper Dropdown(string label, string key)
PanelFluentWrapper DurationInput(string label, string key)
PanelFluentWrapper DynamicTextboxes(string label, string key)
PanelFluentWrapper Filepath(string label, string key)
PanelFluentWrapper IntegerInput(string label, string key)
PanelFluentWrapper NumberInput(string label, string key)
PanelFluentWrapper PillInput(string label, string key)
PanelFluentWrapper Slider(string label, string key)
PanelFluentWrapper Textbox(string label, string key)
PanelFluentWrapper Toggle(string label, string key)
```

## interface FluentConfig.IControlOptions

```csharp
void AllowDuplicates(bool value)
void Color(string hex)
void Default(bool value)
void Default(double value)
void Default(int value)
void Default(string value)
void DefaultByValue(string value)
void DefaultIndex(int index)
void DefaultIndices(int[] indices)
void Hint(string text)
void ItemTemplate(Action<PanelBuilder> build)
void MaxSelected(int n)
void Multiline()
void OnClick(Action<UiContext> callback)
void OnPillAdded(Action<string, CallbackContext> callback)
void OnPillRemoved(Action<string, CallbackContext> callback)
void Options(string[] options)
void OptionsPairs(IEnumerable<ValueTuple<string, string>> pairOptions)
void Password()
void Preset(string[] values)
void Range(double min, double max)
void Range(int min, int max)
void Refresh(Func<string[]> callback)
void RefreshPairs(Func<IEnumerable<ValueTuple<string, string>>> callback)
void ShowWhen(string key)
void Step(double value)
void Text(string caption)
void WithExclusive(string[] options)
void WithPairValue(string valueKey)
void WithPermanentOption(bool value)
void WithStepper(bool value = ...)
```

## class FluentConfig.UiContext

```csharp
bool ShowConfirmDialog(string title, string message, string yesButton, string noButton)
IProgressReporter ShowProgressWindow(string title, string message, string progressLabel, int total)
T Pending(string key)
void Log(string message)
void PatchSchemaNode(string sectionId, string nodeId, SchemaNode node)
void Popup(string title, string message)
void Toast(string message)
```

## class FluentConfig.CallbackContext

```csharp
T GetValue(string key)
void PatchSchemaNode(string sectionId, string nodeId, SchemaNode node)
void RemoveSettingsKeys(string[] keys)
```

## class FluentConfig.Updater.GitHubUpdater

```csharp
static bool EnsureInstalled(string path, string repo)
static bool IsAllowedDownloadUrl(string url)
static bool IsNewer(string candidate, string current)
static string StageUpdate(string downloadUrl, string targetPath)
static UpdateCheckResult CheckForLatestRelease(string repo, string currentVersion)
static UpdateCheckResult CheckForTaggedRelease(string repo, string tagPrefix, string currentVersion)
static UpdateCheckResult CheckForUpdate(string repo, string currentVersion)
static void SetApiBaseUrl(string baseUrl)
static void SetHttpClient(HttpClient client)
```

