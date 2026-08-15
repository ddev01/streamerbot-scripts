# API docs index

Generated / curated references for AI and humans working on Streamer.bot C# in this repo.

| File | Contents |
|------|----------|
| [cph-api.md](cph-api.md) | `CPH` / `IInlineInvokeProxy` method signatures from local Streamer.bot DLLs |
| [fluentconfig-api.md](fluentconfig-api.md) | FluentConfig authoring surface (`Fc`, UI builders, Runtime helpers, Comparator, …) |
| [fluentconfig-guide.md](fluentconfig-guide.md) | How to build settings menus + runtime read/write (`{slug}_settings` / `{slug}_data`) |

Regenerate DLL dumps:

```powershell
.\scripts\Generate-ApiDocs.ps1
# optional: -FluentConfigDll <path-to-built-FluentConfig.dll>
```

IntelliSense (`CPH`, `CPHInlineBase`): copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set `StreamerBotPath` to your Streamer.bot install folder (or set env `STREAMERBOT_PATH`). Reload the C# language server after changing it.

Upstream FluentConfig docs (not copied here): `F:\Dev\SB-FluentConfig\docs\` (especially `guides/LAYOUT.md`, `guides/VISIBILITY.md`, `guides/CONTROLS.md`, `guides/DIALOGS_AND_RUNTIME_VALUES.md`) and examples under `F:\Dev\SB-FluentConfig\examples\tutorial\` (01 → 11).
