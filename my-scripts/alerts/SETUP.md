# Alerts — Streamer.bot setup

Code cannot create actions or commands. Do this in the UIs, then paste the C# from this folder.

This is a SoundAlerts-style GIF/sound overlay. Opening settings installs files under Streamer.bot **`Choppa/`** (not `overlays/`, not FluentConfig, not the GitHub username). `Choppa/media/` is never wiped.

## Giphy (optional)

1. Create a **Web** API key at the Giphy developer dashboard.
2. Overlay tab: paste **Giphy API key**.
3. Alerts tab: create an alert. **GIF / video** has **Giphy** and **Local**. Sound has **File**, a **MyInstants** link (opens in your browser), and **i** (why there is no in-app MP3 search).
4. Click a GIF or pick a local file: it is copied into `Choppa/media/` and the path on the pill points there.

Command alias is optional. Mods can `!alert Name` without spending channel points (testing). `{Alerts} Play` / `PlayNamed` is the same path for other actions.

Settings refs for `{Alerts} 0 Settings` also need `Microsoft.Web.WebView2.Wpf` and `Microsoft.Web.WebView2.Core` (Streamer.bot already ships them; do not copy WebView2 DLLs into `dlls/`).

## Copy-paste names

| Kind         | Name                               |
| ------------ | ---------------------------------- |
| Group        | `{CHOPPA} - Alerts`                |
| Action       | `{Alerts} 0 Settings`              |
| Action       | `{Alerts} 1 Main`                  |
| Action       | `{Alerts} Command`                 |
| Action       | `{Alerts} Play`                    |
| Command name | `{Alerts} Command` (chat `!alert`) |

## 1. Streamer.bot WebSocket

Servers/Clients → **WebSocket Server**:

- Auto-start **on**
- Address `127.0.0.1`
- Port `8080`
- Leave enforce-all-requests **off** unless you know you need it
- Start the server

## 2. Four Streamer.bot actions

Common pattern: a **disabled** Execute C# Code sub-action holds the source; an **Execute C# Method** sub-action calls the method.

Copy `FluentConfig.dll` into Streamer.bot `dlls/` if it is not there already. Do **not** copy WebView2 DLLs.

Do **not** add a third C# action for the overlay helper. `AlertsOverlayDisk` is already at the bottom of `[settings.cs](settings.cs)` and `[main.cs](main.cs)`.

### `{Alerts} 0 Settings`

- Sub-action **Execute C# Code**: paste `[settings.cs](settings.cs)`, then **disable** this sub-action (source only)
- Sub-action **Execute C# Method**: method `Execute`, **Run on UI thread** = on
- Refs: `PresentationFramework`, `PresentationCore`, `WindowsBase`, `System.Net.Http`, `Microsoft.Web.WebView2.Wpf`, `Microsoft.Web.WebView2.Core`, `Newtonsoft.Json.dll`, `FluentConfig.dll` (from `dlls/`)
- No trigger; run it from the actions list to open the menu and install `overlay.html`

### Shared `main.cs` actions

Paste `[main.cs](main.cs)` the same way (disabled code + method). **Disable** Execute C# Code. If that sub-action stays enabled it runs `Execute()` and does nothing useful.

Refs: `System`, `System.Core`, `Newtonsoft.Json.dll`, `FluentConfig.dll`

**Do not** enable Run on UI thread on these.

| Action             | Method      | Trigger                                                                                                         |
| ------------------ | ----------- | --------------------------------------------------------------------------------------------------------------- |
| `{Alerts} 1 Main`  | `OnRedeem`  | Twitch → Channel Reward → **Reward Redemption** → **Any**. Filter in code. Concurrent is fine. Holds `main.cs`. |
| `{Alerts} Command` | `OnCommand` | Twitch command `{Alerts} Command` / `!alert` (mods + broadcaster).                                              |
| `{Alerts} Play`    | `PlayNamed` | None. Other actions call this (set arg `alert` to the pill name).                                               |

## 3. Command

Chat `**!alert**`. Mode **Basic**, location **Start**, extra arguments **on**.

- **Ignore Bot Account**: on
- **Ignore Internal Messages**: on
- Grant: mods + broadcaster

Usage: `!alert` lists names. `!alert Rickroll` plays that pill (or the command alias on the pill).

## 4. OBS

**Local file is correct.** Check **Local file**, browse to:

`{Streamer.bot folder}/Choppa/Alerts/overlay.html`

You do **not** need the `file:///` URL or query string. Defaults are `127.0.0.1:8080`.

Also:

- Width/height = canvas size
- **Shutdown source when not visible**: off
- **Refresh browser when scene becomes active**: off
- **Control audio via OBS**: on

Then:

1. Streamer.bot WebSocket Server must be **started** (step 1)
2. Reopen `**{Alerts} 0 Settings**` after updating the C# (writes `overlay.html` + downloads `streamerbot-client.js` beside it)
3. Right-click the Browser Source → **Refresh**
4. **Test overlay** — you should see centered white text for a few seconds

`streamerbot-client.js` must sit next to `overlay.html`. If it is missing, reopen settings while the PC is online.

Do **not** add one OBS source per GIF.

## 5. Rewards

Create the channel point reward in Streamer.bot (Platforms → Twitch → Channel Point Rewards) so Streamer.bot owns it. Then in the Alerts menu, add a pill and bind that reward (Refresh the dropdown).

Skip-queue is optional here (unlike refunds). If you fulfill from this extension, leave **Fulfill reward after play** on.

## 6. Media

Layout:

- `dlls/FluentConfig.dll` — settings UI (framework)
- `Choppa/Alerts/` — `overlay.html`, `picker.html`, `streamerbot-client.js`
- `Choppa/media/` — GIFs and sounds for any Choppa extension
- GitHub `ddev01` — source and FluentConfig update footer only

**Giphy** and **Local** / **File** copy into `Choppa/media/` immediately. Play also copies if a path still points outside that folder.

If a file is deleted later: the Overlay tab intro lists missing pills when you open settings. Channel-point redeems skip the missing asset and log a warning (no chat spam). `!alert` replies in chat so you notice while testing.

First open after this update copies old `overlays/choppa-alerts` files into `Choppa/` if present. Point OBS at the new overlay.html, then refresh the Browser Source.

Custom videos: convert or use WebM/MP4 the Browser Source can play.

## 7. Updates

FluentConfig shows a GitHub notice in the menu. Paste the new import into the C# sources, then **reopen `{Alerts} 0 Settings`**. That rewrites `overlay.html` when the version stamp changes. User files in `Choppa/media/` stay. If OBS still shows the old player, right-click the Browser Source → Refresh.

`[overlay.html](overlay.html)` in this folder is the readable player source; the C# embed must match it when you change the player.

## 8. `{Alerts} Play`

From another action (e.g. Fire Sale start): Execute C# Method `PlayNamed` on `{Alerts} Play`, or set argument `alert` to the pill name then run that action.

## 9. Test checklist

1. WebSocket server running
2. Open settings → overlay folder exists, Copy OBS URL, Test overlay appears in OBS
3. Bind a reward, redeem → GIF/sound/text on stream
4. `!alert` as mod lists names; `!alert <name>` plays
5. Second redeem while the first plays → queued, not overlapped
6. Reopen settings after bumping version → `overlay.version` updates, `Choppa/media/` untouched
