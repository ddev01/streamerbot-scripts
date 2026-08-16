# TTS — Streamer.bot setup

Code cannot create Twitch rewards or Streamer.bot actions. Do this in the UIs, then paste the C# from this folder.

Suggested costs: **TTS Message 400**, **Unlock TTS Voice 8,000–10,000**. No Twitch global cooldown and no max-per-stream. Per-user spacing is the **30s cooldown in TTS settings** (refund if they redeem early).

Twitch reward **prompts are plain text**. URLs are not clickable. Put the alias list in the prompt.

## 1. Twitch rewards

Create **two** custom rewards. Both need:

- **Require Viewer to Enter Text** (on)
- **Skip Reward Requests Queue** (**off**) — if this is on, Twitch auto-fulfills and `TwitchRedemptionCancel` **cannot refund**
- No global cooldown, no max per stream / per user (the bot handles cooldown)

### TTS Message (speak)

- Title example: `TTS Message`
- Cost: `400`
- Prompt example: `Default English. Own a voice? Start with alias:style then a space, e.g. ch:angry hello or fr: bonjour. Sticky: !tts set fr`

### Unlock TTS Voice

- Title example: `Unlock TTS Voice`
- Cost: `8000`–`10000`
- Prompt example: `Type a voice name or alias: English en, French fr, Spanish es, Chinese ch, German ge. Permanent. Duplicate unlocks are refunded.`

Optional: put both in the same Twitch reward group so Fire Sale can discount them. `!tts off` still pauses by reward id.

## 2. Four Streamer.bot actions

Common pattern: a **disabled** Execute C# Code sub-action holds the source; an **Execute C# Method** sub-action calls the method.

Copy `FluentConfig.dll` into Streamer.bot `dlls/` if it is not there already. Do **not** copy WebView2 DLLs.

### TTS — Settings

- Sub-action **Execute C# Code**: paste [`settings.cs`](settings.cs), then disable this sub-action (source only)
- Sub-action **Execute C# Method**: method `Execute`, **Run on UI thread** = on
- Refs: `PresentationFramework`, `PresentationCore`, `WindowsBase`, `FluentConfig.dll` (from `dlls/`)
- No trigger required; run it from the actions list to open the menu

### TTS — Speak

- Paste [`main.cs`](main.cs) the same way (disabled code + method)
- **Disable** Execute C# Code (source only). If that sub-action stays enabled it runs `Execute()` and does not speak.
- Sub-action **Execute C# Method**: method **`TtsSpeak`** (not `Execute`)
- Refs: `System`, `System.Core`, `System.Net.Http`, `System.Windows.Forms`, `Newtonsoft.Json.dll`, `FluentConfig.dll`
- **Do not allow concurrent** / queue this action so clips play one after another (`PlaySound` waits until finished)
- Trigger: **Twitch Reward Redemption** filtered to **TTS Speak** only (not “any reward”)

### TTS — Unlock

- Same `main.cs` source (or share the file in Streamer.bot if you keep one copy)
- Method: **`TtsUnlock`**
- Same refs as Speak
- Concurrent is fine
- Trigger: **Twitch Reward Redemption** filtered to **Unlock TTS Voice** only

In **TTS Settings**, pick both rewards in the two dropdowns (Refresh if the list is empty), then Save. Unlock will ignore Speak redemptions; without that, a shared “any reward” trigger treats `hello world` as a voice name.

### TTS — Command

- Same `main.cs`
- Method: `TtsCommand`
- Same refs as Speak
- Concurrent is fine
- Trigger: **Twitch Command** `!tts` with extra arguments included (`rawInput` is the rest of the line)

`!tts hello` does **not** speak. Speaking is reward-only.

Commands:

- `!tts` — help
- `!tts voices` / `!tts inventory` — owned voices and aliases
- `!tts set <alias>` (also `!tts voice`, `!tts set voice`) — sticky default; `default` or `en` for free English
- `!tts off` / `!tts on` — broadcaster or mod; pauses / unpauses both rewards

## 3. TTS settings menu

Run **TTS — Settings**, Save once (seeds English/French/Spanish/Chinese/German). Then:

1. Paste the Azure Speech **key** (and region, default `northeurope`)
2. Refresh and pick the two rewards
3. Confirm Azure voice IDs and style lists on each pill (edit to taste)
4. Default voice alias should be `en` (free English, **no styles** until they unlock English)

## 4. How viewers use it

| Typed in TTS Message | Result |
| --- | --- |
| `hello there` | Sticky voice, or default English, no style |
| `ch: hello there` | Chinese, default style (must own `ch`) |
| `ch:angry hello there` | Chinese + angry (must own `ch`, style must be on that voice) |
| `angry: hello there` | Style only; needs a sticky **owned** voice that lists `angry` |

Unpaid / unknown prefix or style, empty text, URLs, over max length, cooldown, or TTS paused → **refund** + short chat. Successful speak is **fulfilled** then synthesized.

Unlock: type `French` or `fr`. Already owned or unknown → refund. Success → chat with styles, prefix example, `!tts set`.

## 5. Test checklist

1. Redeem TTS Message with `hello` → English audio, points spent
2. Redeem with `ch: nihao` before unlocking Chinese → refund, no audio
3. Redeem Unlock with `ch` or `Chinese` → owned; chat explains prefix + `!tts set ch`
4. Redeem TTS Message with `ch:angry hello` → Chinese angry (if that style is on the pill)
5. `!tts set ch` then TTS Message `hello` → Chinese without a prefix
6. Redeem TTS Message twice within 30s → second refund, seconds left
7. `!tts off` → both rewards greyed/paused; redeem (if still possible) refunds; `!tts on` restores
8. Confirm skip-queue is still **off** so refunds actually return points
