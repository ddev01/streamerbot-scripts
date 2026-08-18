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
                            .Default(50)))
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
                            .Hint("Per-user cooldown after TTS finishes playing. Refunds with seconds left. Stored in a non-persistent Twitch user var (clears when Streamer.bot restarts).")
                            .Range(0, 3600)
                            .Default(15)
                            .Size("w-fit min-w-20")
                        .IntegerInput("Max message length", "max_chars")
                            .Hint("Spoken text only (after the prefix).")
                            .Range(1, 2000)
                            .Default(250)
                            .Size("w-fit min-w-20"))
                    .Toggle("Block URLs", "block_urls")
                        .Hint("Refund messages that look like links.")
                        .Default(true))
                .Section("Anti-spam", "Anti-spam", s => s
                    .Intro("All checks start **off**. Twitch blocked terms and AutoMod do not reliably filter Channel Points TTS text; these refund before speak. Chat uses one generic line; the log names the rule.")
                    .Toggle("Repeated characters", "spam_char_repeat")
                        .Hint("Blocks runs like aaaa or !!!!!!.")
                        .Default(false)
                    .WithVisibility("spam_char_repeat", inner => inner
                        .IntegerInput("Max same character in a row", "spam_char_repeat_max")
                            .Hint("Block if any character repeats more than this.")
                            .Range(2, 40)
                            .Default(8)
                            .Size("w-fit min-w-20"))
                    .Toggle("Repeated word", "spam_word_repeat")
                        .Hint("Blocks the same word copied back to back (spam spam spam…).")
                        .Default(false)
                    .WithVisibility("spam_word_repeat", inner => inner
                        .IntegerInput("Max consecutive copies", "spam_word_repeat_max")
                            .Hint("Block if one word appears more than this many times in a row.")
                            .Range(2, 50)
                            .Default(5)
                            .Size("w-fit min-w-20"))
                    .Toggle("Repeated phrase", "spam_phrase")
                        .Hint("Blocks copy-paste and looping phrases, e.g. “you suck hard” five times.")
                        .Default(false)
                    .WithVisibility("spam_phrase", inner => inner
                        .Grid("grid-cols-2 items-center", g => g
                            .IntegerInput("Max words in phrase", "spam_phrase_max_words")
                                .Hint("Longest repeating chunk to look for (1 through this).")
                                .Range(1, 20)
                                .Default(6)
                                .Size("w-fit min-w-20")
                            .IntegerInput("Min repeats to block", "spam_phrase_min_repeats")
                                .Hint("Block if that chunk repeats this many times in a row.")
                                .Range(2, 20)
                                .Default(5)
                                .Size("w-fit min-w-20")))
                    .Toggle("Low unique-word ratio", "spam_unique")
                        .Hint("Blocks when almost every word is a repeat. Ignored on short messages.")
                        .Default(false)
                    .WithVisibility("spam_unique", inner => inner
                        .Grid("grid-cols-2 items-center", g => g
                            .IntegerInput("Min words to check", "spam_unique_min_words")
                                .Hint("Skip this rule if the message has fewer words.")
                                .Range(3, 100)
                                .Default(8)
                                .Size("w-fit min-w-20")
                            .NumberInput("Min unique ratio", "spam_unique_min_ratio")
                                .Hint("0.35 = at least 35% of words must be distinct.")
                                .Range(0.05, 1.0)
                                .Step(0.05)
                                .Default(0.35)
                                .Size("w-fit min-w-20")))
                    .Toggle("Blocked words", "spam_blocked_words")
                        .Hint("TTS-only list. Does not sync with Twitch chat blocked terms. Whole words, or multi-word phrases.")
                        .Default(false)
                    .WithVisibility("spam_blocked_words", inner => inner
                        .DynamicTextboxes("Words and phrases", "blocked_words")
                            .Hint("Case-insensitive. “suck” matches a word; “you suck” matches that phrase. Does not match inside other words (ass vs class).")
                            .AllowDuplicates(false)))
                .Section("Voices", "Voices", v => v
                    .Intro("Pill **name** is the short alias viewers type (`fr hello`). " + "Display name is what they can type on the unlock reward (`French`). " + "Default voice is free (no prefix). If you delete it, the next remaining pill becomes default. " + "Leave Styles empty. If you ever fill them, viewers pick one with `fr::cheerful hello`.")
                    .PillInput("Voices", "voices")
                        .Hint("Add an alias and press Enter. Example: fr")
                        .ItemTemplate(item => item
                            .Title("Voice: {name}")
                            .Textbox("Display name", "{name}_display")
                                .Hint("Shown in chat and accepted on unlock, e.g. French.")
                            .Textbox("Azure voice ID", "{name}_azure")
                                .Hint("Full Neural name, e.g. fr-FR-DeniseNeural.")
                            .Textbox("Styles", "{name}_styles")
                                .Hint("Leave empty. Azure style ids only if you want alias::style later, e.g. cheerful, sad.")
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
                        .Default("@{user} TTS is on cooldown ({seconds}s left).")
                    .Textbox("Paused refund", "msg_paused")
                        .Default("@{user} TTS is paused right now.")
                    .Textbox("Unpaid voice refund", "msg_unpaid")
                        .Default("@{user} You don't own {alias}. Unlock it with Unlock TTS Voice.")
                    .Textbox("Unknown prefix refund", "msg_unknown_prefix")
                        .Default("@{user} Unknown voice '{prefix}'. Unlock names: {voices}.")
                    .Textbox("Unknown style refund", "msg_unknown_style")
                        .Default("@{user} {alias} has no style '{style}'.")
                    .Textbox("Empty message refund", "msg_empty")
                        .Default("@{user} Type a message after the prefix.")
                    .Textbox("Too long refund", "msg_too_long")
                        .Default("@{user} TTS is too long (max {max} characters).")
                    .Textbox("URL refund", "msg_url")
                        .Default("@{user} Links are not allowed in TTS.")
                    .Textbox("Spam refund", "msg_spam")
                        .Default("@{user} That TTS looks like spam.")
                    .Textbox("Unlock success", "msg_unlock_ok")
                        .Multiline()
                        .Default("@{user} unlocked {alias}. TTS prefix: {prefix}hello. Sticky: !tts set {alias}")
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
        o["volume"] = 50;
        o["cooldown_seconds"] = 15;
        o["max_chars"] = 250;
        o["block_urls"] = true;
        o["spam_char_repeat"] = false;
        o["spam_char_repeat_max"] = 8;
        o["spam_word_repeat"] = false;
        o["spam_word_repeat_max"] = 5;
        o["spam_phrase"] = false;
        o["spam_phrase_max_words"] = 6;
        o["spam_phrase_min_repeats"] = 5;
        o["spam_unique"] = false;
        o["spam_unique_min_words"] = 8;
        o["spam_unique_min_ratio"] = 0.35;
        o["spam_blocked_words"] = false;
        o["blocked_words"] = new JArray();
        o["default_voice"] = "en";
        o["voices"] = new JArray("en", "fr", "es", "ch", "ge");
        WriteVoice(o, "en", "English", "en-GB-Ollie:DragonHDLatestNeural", "angry, cheerful, excited, hopeful, sad, shouting, terrified, unfriendly, whispering");
        WriteVoice(o, "fr", "French", "fr-FR-Remy:DragonHDLatestNeural", "");
        WriteVoice(o, "es", "Spanish", "es-ES-AlvaroNeural", "cheerful, sad");
        WriteVoice(o, "ch", "Chinese", "zh-CN-XiaoxiaoMultilingualNeural", "");
        WriteVoice(o, "ge", "German", "de-DE-FlorianMultilingualNeural", "");
    }

    private static void WriteVoice(JObject o, string alias, string display, string azure, string styles)
    {
        o[alias + "_display"] = display;
        o[alias + "_azure"] = azure;
        o[alias + "_styles"] = styles;
    }
}