using FluentConfig;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

// Refs: PresentationFramework, PresentationCore, WindowsBase,
//       FluentConfig.dll (Streamer.bot dlls/).
// Execute C# Method + Run on UI thread.
#if EXTERNAL_EDITOR
public class TtsSettings : CPHInlineBase
#else
public class CPHInline
#endif
{
    private static class ExtensionInfo
    {
        public const string Title = "TTS";
        public const string Version = "1.0.0";
        public const string Repo = "ddev01/streamerbot-scripts";
        public const string TagPrefix = "tts";
    }

    public bool Execute()
    {
        CPH.LogInfo($"[TTS] Opening settings ({ExtensionInfo.Title} v{ExtensionInfo.Version}).");
        if (!Fc.HasSavedSettings(CPH, ExtensionInfo.Title))
            Fc.SaveSettings(CPH, ExtensionInfo.Title, SeedDefaults);
        Fc.Open(CPH, ExtensionInfo.Title, ExtensionInfo.Version, ui => ui
                .WithExtensionUpdateNotice(ExtensionInfo.Repo, ExtensionInfo.Version, tagPrefix: ExtensionInfo.TagPrefix)
                .Section("Azure", "Azure", a => a
                    .Intro("Synthesize with Azure Neural TTS (same path as the latency test). " + "Paste a Speech resource key. See SETUP.md for Streamer.bot wiring.")
                    .Textbox("Subscription key", "azure_key")
                        .Hint("Primary key from the Azure Speech resource.")
                        .Password()
                    .Textbox("Backup key", "azure_key2")
                        .Hint("Optional second key if the primary is rotated.")
                        .Password()
                    .Grid("grid-cols-2 items-center", g => g
                        .Textbox("Region", "region")
                            .Hint("Short region name, e.g. northeurope.")
                            .Default("northeurope")
                        .Slider("Volume", "volume")
                            .Hint("Playback volume (0–100).")
                            .Range(0, 100)
                            .Default(100)))
                .Section("Rewards", "Rewards", r => r
                    .Intro("Pick the two Twitch rewards. Both must require viewer text and must **not** skip the redemption queue, " + "or refunds will not work. Suggested costs: TTS 400, Unlock 8000–10000.")
                    .Dropdown("TTS Message reward", "speak_reward_display")
                        .Hint("Viewers type the line to speak. Refresh loads titles from Twitch.")
                        .Searchable()
                        .AllowCustom()
                        .WithPairValue("speak_reward_id")
                        .Refresh(ListRewardPairs)
                    .Dropdown("Unlock TTS Voice reward", "unlock_reward_display")
                        .Hint("Viewers type a voice name or alias to buy it permanently.")
                        .Searchable()
                        .AllowCustom()
                        .WithPairValue("unlock_reward_id")
                        .Refresh(ListRewardPairs)
                    .Grid("grid-cols-2 items-center", g => g
                        .IntegerInput("User cooldown (seconds)", "cooldown_seconds")
                            .Hint("Per-user cooldown after a successful TTS. Enforced in code; refund if still hot.")
                            .Range(0, 3600)
                            .Default(30)
                            .Size("w-fit min-w-20")
                        .IntegerInput("Max message length", "max_chars")
                            .Hint("Spoken text only (after the prefix).")
                            .Range(1, 2000)
                            .Default(250)
                            .Size("w-fit min-w-20"))
                    .Toggle("Block URLs", "block_urls")
                        .Hint("Refund messages that look like links.")
                        .Default(true))
                .Section("Voices", "Voices", v => v
                    .Intro("Pill **name** is the short alias viewers type (`fr`, `ch`). " + "Display name is what they can type on the unlock reward (`French`). " + "Styles are comma-separated Azure style ids for that voice only. " + "Default voice is the free TTS voice (no styles until unlocked). If you delete it, the next remaining pill becomes default.")
                    .PillInput("Voices", "voices")
                        .Hint("Add an alias and press Enter. Example: fr")
                        .ItemTemplate(item => item
                            .Title("Voice: {name}")
                            .Textbox("Display name", "{name}_display")
                                .Hint("Shown in chat and accepted on unlock, e.g. French.")
                            .Textbox("Azure voice ID", "{name}_azure")
                                .Hint("Full Neural name, e.g. fr-FR-DeniseNeural.")
                            .Textbox("Styles", "{name}_styles")
                                .Hint("Comma-separated: angry, cheerful, sad, whispering. Leave empty if none.")
                        )
                        .OnPillAdded((name, ctx) => SyncDefaultVoice(ctx, null))
                        .OnPillRemoved((name, ctx) =>
                        {
                            ctx.RemoveSettingsKeys(name + "_display", name + "_azure", name + "_styles");
                            SyncDefaultVoice(ctx, name);
                        })
                    .Dropdown("Default voice", "default_voice")
                        .Hint("Free voice for TTS Speak when they do not prefix another owned voice. Refresh after adding pills.")
                        .Searchable()
                        .AllowCustom()
                        .Refresh(ListVoiceAliases)
                        .Default("en"))
                .Section("Messages", "Messages", m => m
                    .Intro("Short chat lines. Placeholders: `{user}` `{alias}` `{style}` `{seconds}` `{styles}` `{prefix}` `{voices}` `{owned}` `{sticky}`.")
                    .Textbox("Help (!tts)", "msg_help")
                        .Multiline()
                        .Default("@{user} Redeem TTS Message to speak (default English). " + "Prefix a voice you own: {prefix}hello. Unlock voices with Unlock TTS Voice. " + "!tts voices | !tts set <alias>")
                    .Textbox("Cooldown refund", "msg_cooldown")
                        .Default("@{user} TTS cooldown: {seconds}s left.")
                    .Textbox("Paused refund", "msg_paused")
                        .Default("@{user} TTS is paused right now.")
                    .Textbox("Unpaid voice refund", "msg_unpaid")
                        .Default("@{user} You don't own {alias}. Unlock it with Unlock TTS Voice.")
                    .Textbox("Unknown prefix refund", "msg_unknown_prefix")
                        .Default("@{user} Unknown voice '{prefix}'. Unlock names: {voices}.")
                    .Textbox("Unknown style refund", "msg_unknown_style")
                        .Default("@{user} {alias} styles: {styles}.")
                    .Textbox("Empty message refund", "msg_empty")
                        .Default("@{user} Type a message after the prefix.")
                    .Textbox("Too long refund", "msg_too_long")
                        .Default("@{user} TTS is too long (max {max} characters).")
                    .Textbox("URL refund", "msg_url")
                        .Default("@{user} Links are not allowed in TTS.")
                    .Textbox("Unlock success", "msg_unlock_ok")
                        .Multiline()
                        .Default("@{user} unlocked {alias}. Styles: {styles}. " + "TTS prefix: {prefix}hello (or {prefix}angry hello if that style exists). " + "Sticky: !tts set {alias}")
                    .Textbox("Already owned refund", "msg_unlock_owned")
                        .Default("@{user} You already own {alias}.")
                    .Textbox("Unknown unlock refund", "msg_unlock_unknown")
                        .Default("@{user} Unknown voice. Try: {voices}")
                    .Textbox("Set sticky ok", "msg_set_ok")
                        .Default("@{user} Default TTS voice is now {alias}.")
                    .Textbox("Set sticky need own", "msg_set_need_own")
                        .Default("@{user} Unlock {alias} first, or use !tts set default.")
                    .Textbox("Need mod", "msg_need_mod")
                        .Default("@{user} Mods only.")
                    .Textbox("TTS off", "msg_off")
                        .Default("TTS rewards paused.")
                    .Textbox("TTS on", "msg_on")
                        .Default("TTS rewards are live.")
                    .Textbox("Speak is reward-only", "msg_speak_reward_only")
                        .Default("@{user} Speak with the TTS Message reward, not !tts. Try !tts for help.")
                    .Textbox("Azure failed (already charged)", "msg_synth_fail")
                        .Default("@{user} TTS failed to play. Try again in a bit.")));
        return true;
    }

    private (string value, string label)[] ListRewardPairs()
    {
        try
        {
            var rewards = CPH.TwitchGetRewards();
            if (rewards == null || rewards.Count == 0)
                return Array.Empty<(string, string)>();
            return rewards.Where(r => r != null && !string.IsNullOrWhiteSpace(r.Id)).Select(r => (r.Id, string.IsNullOrWhiteSpace(r.Title) ? r.Id : r.Title)).ToArray();
        }
        catch
        {
            return Array.Empty<(string, string)>();
        }
    }

    private string[] ListVoiceAliases()
    {
        var aliases = Fc.GetSetting(CPH, ExtensionInfo.Title, "voices", Array.Empty<string>());
        if (aliases == null || aliases.Length == 0)
            return Array.Empty<string>();
        return aliases.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToArray();
    }

    private void SyncDefaultVoice(CallbackContext ctx, string removed)
    {
        var raw = ctx.GetValue<string[]>("voices") ?? Array.Empty<string>();
        var voices = raw
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Where(v => removed == null || !string.Equals(v, removed, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string current = ctx.GetValue<string>("default_voice") ?? "";
        bool missing = string.IsNullOrWhiteSpace(current)
            || (removed != null && string.Equals(current, removed, StringComparison.OrdinalIgnoreCase))
            || !voices.Any(v => string.Equals(v, current, StringComparison.OrdinalIgnoreCase));
        string next = voices.Length == 0 ? "" : (missing ? voices[0] : current.Trim());
        Fc.SetSetting(CPH, ExtensionInfo.Title, "default_voice", next);
    }

    private static void SeedDefaults(JObject o)
    {
        o["region"] = "northeurope";
        o["volume"] = 100;
        o["cooldown_seconds"] = 30;
        o["max_chars"] = 250;
        o["block_urls"] = true;
        o["default_voice"] = "en";
        o["voices"] = new JArray("en", "fr", "es", "ch", "ge");
        WriteVoice(o, "en", "English", "en-US-AriaNeural", "angry, cheerful, excited, hopeful, sad, shouting, terrified, unfriendly, whispering");
        WriteVoice(o, "fr", "French", "fr-FR-DeniseNeural", "cheerful, sad");
        WriteVoice(o, "es", "Spanish", "es-ES-ElviraNeural", "cheerful, sad");
        WriteVoice(o, "ch", "Chinese", "zh-CN-XiaoxiaoNeural", "angry, cheerful, fearful, gentle, sad, serious, whispering");
        WriteVoice(o, "ge", "German", "de-DE-KatjaNeural", "");
    }

    private static void WriteVoice(JObject o, string alias, string display, string azure, string styles)
    {
        o[alias + "_display"] = display;
        o[alias + "_azure"] = azure;
        o[alias + "_styles"] = styles;
    }
}