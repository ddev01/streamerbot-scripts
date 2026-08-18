# Refund Rewards — Streamer.bot setup

Code cannot create actions, commands, or timers. Do this in the UIs, then paste the C# from this folder.

Rewards must **not** skip the redemption queue, or Twitch auto-fulfills and `TwitchRedemptionCancel` cannot refund.

## Copy-paste names

| Kind | Name |
|------|------|
| Group | `{CHOPPA} - Refund Rewards` |
| Action | `{Refund Rewards} 0 Settings` |
| Action | `{Refund Rewards} 1 Main` |
| Action | `{Refund Rewards} Updated` |
| Action | `{Refund Rewards} Command` |
| Action | `{Refund Rewards} Cancel` |
| Action | `{Refund Rewards} Commit` |
| Timer | `{Refund Rewards} Commit` |
| Command name | `{Refund Rewards} Command` (chat `!refund`) |
| Command name | `{Refund Rewards} Cancel` (chat `!refundcancel`) |

## 1. Timer (do this first)

Settings → Timers → Add:

- Name: `{Refund Rewards} Commit`
- **Enabled**: off
- **Repeat**: off
- Interval: anything (the script overwrites it)
- Lines: `0`

Right-click the timer → **Copy Timer Id**. Paste that GUID into `CommitTimerId` at the top of [`main.cs`](main.cs). Then paste `main.cs` into Streamer.bot (below).

When you export the extension, **Add to Export** this timer with the actions. Importers keep the same timer GUID, so they do not need a settings dropdown.

If someone creates their own timer instead of importing yours, they get a **new** id and must paste it into `CommitTimerId`.

## 2. Six Streamer.bot actions

Common pattern: a **disabled** Execute C# Code sub-action holds the source; an **Execute C# Method** sub-action calls the method.

Copy `FluentConfig.dll` into Streamer.bot `dlls/` if it is not there already. Do **not** copy WebView2 DLLs.

### `{Refund Rewards} 0 Settings`

- Sub-action **Execute C# Code**: paste [`settings.cs`](settings.cs), then **disable** this sub-action (source only)
- Sub-action **Execute C# Method**: method `Execute`, **Run on UI thread** = on
- Refs: `PresentationFramework`, `PresentationCore`, `WindowsBase`, `FluentConfig.dll` (from `dlls/`)
- No trigger; run it from the actions list to open the menu

### Shared `main.cs` actions

Paste [`main.cs`](main.cs) the same way (disabled code + method). **Disable** Execute C# Code. If that sub-action stays enabled it runs `Execute()` / `Refund()` on every run.

Refs: `System`, `System.Core`, `Newtonsoft.Json.dll`, `FluentConfig.dll`

**Do not** enable Run on UI thread on these.

| Action | Method | Trigger |
|--------|--------|---------|
| `{Refund Rewards} 1 Main` | `Track` | Twitch → Channel Reward → **Reward Redemption** → **Any**. Concurrent is fine. Holds `main.cs`. |
| `{Refund Rewards} Updated` | `OnUpdated` | Twitch → Channel Reward → **Reward Redemption Updated** → **Any**. |
| `{Refund Rewards} Command` | `Refund` | Twitch command `{Refund Rewards} Command` / `!refund` (see below). |
| `{Refund Rewards} Cancel` | `Cancel` | Twitch command `{Refund Rewards} Cancel` / `!refundcancel`. |
| `{Refund Rewards} Commit` | `Commit` | Core → **Timed Actions** → `{Refund Rewards} Commit`. |

## 3. Commands

Chat **`!refund`** and **`!refundcancel`**. Mode **Basic**, location **Start**. Extra arguments on for `!refund`.

**Commands field: one alias per line, no blank lines.** An empty line is a second trigger. Empty aliases match every chat message (including `a` and the bot’s own replies), which is what caused the loop.

Same as Tawmae:

- **Ignore Bot Account**: on — so usage can say `!refund` / `!refundcancel` without retriggering
- **Ignore Internal Messages**: on
- Grant: mods + broadcaster

Do **not** use Location = Anywhere. Do **not** use Exact for `!refund` (`!refund @user` would not match). Regex is unnecessary if there is no blank alias.

- `{Refund Rewards} Command` → method **`Refund`**
- `{Refund Rewards} Cancel` → method **`Cancel`**

Disable Execute C# Code on these actions (source only).

Usage: `!refund @user [count]` — count defaults to **1**.

## 4. Settings menu

Run **`{Refund Rewards} 0 Settings`**, Save once:

- **Mod self refund** (off) — mods cannot refund themselves; you still can
- **Print individual rewards** (on) — stacked names in chat
- **Confirm delay** (10s)
- **Max refund count** (25)

## 5. How it works

Track keeps unfulfilled redemptions (ids + cost at redeem time). `!refund` queues the last N for that user, announces totals, and enables the timer. `!refundcancel` (you, any mod, or the mod who queued it) aborts. When the timer fires, Commit calls Twitch cancel. Already fulfilled/missing rows are skipped.

Skip-queue rewards never stay refundable: Updated removes them when Twitch auto-completes.

## 6. Export

Add to export: all six actions, both commands, the timer, and the action group if you use one.

## 7. Test checklist

1. Redeem a **non-skip-queue** reward as a viewer
2. `!refund @thatUser` as a mod → chat queues, wait → points returned
3. Queue again and `!refundcancel` within the delay → no refund
4. Mod `!refund @themselves` → denied unless Mod self refund is on
5. Broadcaster `!refund @themselves` → allowed
6. `!refund @user 5` stacks duplicate titles (`Name ×2 (800)`)
7. Second `!refund` while one is queued → busy message
