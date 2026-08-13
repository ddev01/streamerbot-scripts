# FluentConfig API Reference (generated)

Source: `F:\Dev\SB-FluentConfig\FluentConfig\Host\bin\Debug\net481\FluentConfig.dll`
Generated: 2026-08-13
Author source/docs: `F:\Dev\SB-FluentConfig`

Authoring surface only. Prefer `Fc.*` in action scripts. Control factories are on `SectionBuilder` / `PanelBuilder`; option methods chain via `IControlOptions`.
Layout: `Grid`/`Row`/`Size` use 1:1 Tailwind class names (see [fluentconfig-guide.md](fluentconfig-guide.md) and `F:\Dev\SB-FluentConfig\docs\guides\LAYOUT.md`).
Runtime helpers (`LoadSettings`/`SetSetting`/`LoadData`/`Logger`/`CaptureEvent`/`ApplyTemplate`): [fluentconfig-guide.md](fluentconfig-guide.md) and `F:\Dev\SB-FluentConfig\docs\guides\DIALOGS_AND_RUNTIME_VALUES.md`.
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
static bool IsOpen { get }
static bool AlreadyOpened(string title = ..., string version = ...)
static bool HasSavedSettings(IInlineInvokeProxy cph, string title)
static EventContext CaptureEvent(IInlineInvokeProxy cph)
static ExtensionLogger Logger(IInlineInvokeProxy cph, string title, string version, IEnumerable<string> extraSensitiveKeys = ...)
static string ApplyTemplate(string template, EventContext ev)
static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> vars)
static string DataKeyFor(string title)
static string GetVersion()
static string KeyFor(string title, string suffix)
static string SettingsKeyFor(string title)
static string SlugFor(string title)
static T LoadData(IInlineInvokeProxy cph, string title, Action<T> validate = ...)
static T LoadSettings(IInlineInvokeProxy cph, string title, Action<T> validate = ...)
static TValue GetData(IInlineInvokeProxy cph, string title, string jsonPath, TValue defaultValue = ...)
static TValue GetSetting(IInlineInvokeProxy cph, string title, string jsonKey, TValue defaultValue = ...)
static void SaveData(IInlineInvokeProxy cph, string title, T data)
static void SaveSettings(IInlineInvokeProxy cph, string title, Action<JObject> mutate)
static void SetData(IInlineInvokeProxy cph, string title, string jsonPath, TValue value)
static void SetLogCallback(Action<string> callback)
static void SetSetting(IInlineInvokeProxy cph, string title, string jsonKey, TValue value)
static void SetWindowClosedCallback(Action<double, double, double, double> callback)
static void SetWindowClosedCallback(Action<double, double> callback)
```

## class FluentConfig.Fc

```csharp
static bool IsOpen { get }
static bool AlreadyOpened(string title = ..., string version = ...)
static bool HasSavedSettings(IInlineInvokeProxy cph, string title)
static EventContext CaptureEvent(IInlineInvokeProxy cph)
static ExtensionLogger Logger(IInlineInvokeProxy cph, string title, string version, IEnumerable<string> extraSensitiveKeys = ...)
static string ApplyTemplate(string template, EventContext ev)
static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> vars)
static string DataKeyFor(string title)
static string GetVersion()
static string KeyFor(string title, string suffix)
static string SettingsKeyFor(string title)
static string SlugFor(string title)
static T LoadData(IInlineInvokeProxy cph, string title, Action<T> validate = ...)
static T LoadSettings(IInlineInvokeProxy cph, string title, Action<T> validate = ...)
static TValue GetData(IInlineInvokeProxy cph, string title, string jsonPath, TValue defaultValue = ...)
static TValue GetSetting(IInlineInvokeProxy cph, string title, string jsonKey, TValue defaultValue = ...)
static void Open(IInlineInvokeProxy cph, string title, string version, Action<FluentConfigUi> build, string iconPath = ...)
static void SaveData(IInlineInvokeProxy cph, string title, T data)
static void SaveSettings(IInlineInvokeProxy cph, string title, Action<JObject> mutate)
static void SetData(IInlineInvokeProxy cph, string title, string jsonPath, TValue value)
static void SetSetting(IInlineInvokeProxy cph, string title, string jsonKey, TValue value)
static void ShowOrFocus(IInlineInvokeProxy cph, string title, string version, Action<FluentConfigUi> build, string iconPath = ...)
```

## class FluentConfig.SectionBuilder

```csharp
SectionBuilder ConnectionStatus(string label, string initialStatus, string buttonText = ..., Action<UiContext> onClick = ...)
SectionBuilder Grid(string spec, Action<PanelBuilder> build)
SectionBuilder Intro(string text)
SectionBuilder RepeatFor(string driverKey, Action<PanelBuilder, int> build, Nullable<int> max = ..., int startIndex = ...)
SectionBuilder Row(Action<PanelBuilder> build)
SectionBuilder Row(string spec, Action<PanelBuilder> build)
SectionBuilder Separator()
SectionBuilder Title(string text)
SectionBuilder WithRepeatableRows(string saveKey, Action<PanelBuilder> buildRow)
SectionBuilder WithVisibility(string key, Comparator op, int value, Action<PanelBuilder> build)
SectionBuilder WithVisibility(string key, Comparator op, string compareKey, Action<PanelBuilder> build)
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
PanelBuilder Grid(string spec, Action<PanelBuilder> build)
PanelBuilder Intro(string text)
PanelBuilder RepeatFor(string driverKey, Action<PanelBuilder, int> build, Nullable<int> max = ..., int startIndex = ...)
PanelBuilder Row(Action<PanelBuilder> build)
PanelBuilder Row(string spec, Action<PanelBuilder> build)
PanelBuilder Separator()
PanelBuilder Title(string text)
PanelBuilder WithRepeatableRows(string saveKey, Action<PanelBuilder> buildRow)
PanelBuilder WithVisibility(string key, Comparator op, int value, Action<PanelBuilder> build)
PanelBuilder WithVisibility(string key, Comparator op, string compareKey, Action<PanelBuilder> build)
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
void ShowWhen(string key, Comparator op, int value)
void ShowWhen(string key, Comparator op, string compareKey)
void Size(string spec)
void Step(double value)
void Text(string caption)
void WithExclusive(string[] options)
void WithPairValue(string valueKey)
void WithPermanentOption(bool value)
void WithStepper(bool value = ...)
```

## enum FluentConfig.Comparator

```csharp
GreaterOrEqual
GreaterThan
LessOrEqual
LessThan
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

## class FluentConfig.KnownBots

```csharp
static bool IsKnownBot(string user)
static IReadOnlyList<string> GetKnownBots()
```

## class FluentConfig.Runtime.ExtensionLogger

```csharp
void Error(string message, string member = ...)
void Failed(string operation, Exception ex, string member = ...)
void Info(string message, string member = ...)
void Init(string member = ...)
void LogSettings(JObject settings, string member = ...)
void Warn(string message, string member = ...)
```

## class FluentConfig.Runtime.EventContext

```csharp
string ActionName { get }
string Command { get }
bool IsModerator { get }
bool IsSubscribed { get }
bool IsTest { get }
bool IsVip { get }
string Message { get }
string MessageId { get }
string RawInput { get }
string TriggerName { get }
string User { get }
string UserId { get }
string UserName { get }
string UserType { get }
static EventContext Capture(IInlineInvokeProxy cph)
```

## class FluentConfig.Runtime.MessageTemplates

```csharp
static string Apply(string template, EventContext ev)
static string Apply(string template, IReadOnlyDictionary<string, string> vars)
static string SanitizeMention(string userName)
```

## class FluentConfig.Runtime.SettingsRedaction

```csharp
static JObject RedactObject(JObject source, IEnumerable<string> extraSensitiveKeys = ...)
static JToken Redact(JToken source, IEnumerable<string> extraSensitiveKeys = ...)
```

## class FluentConfig.Updater.GitHubUpdater

```csharp
static string ApiBaseUrl { get }
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

