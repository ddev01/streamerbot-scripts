using FluentConfig;
using FluentConfig.Runtime;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// Refs: System, System.Core, System.Net.Http, System.Windows.Forms, Newtonsoft.Json.dll, FluentConfig.dll (Streamer.bot dlls/).
#if EXTERNAL_EDITOR
public class TtsMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "TTS";
    private const string Version = "1.0.0";
    private static readonly string OwnedVar = Fc.KeyFor(Title, "owned");
    private static readonly string StickyVar = Fc.KeyFor(Title, "sticky");
    private static readonly string LastMsVar = Fc.KeyFor(Title, "last_ms");
    private const string FallbackAzureVoice = "en-GB-Ollie:DragonHDLatestNeural";
    private static readonly object CooldownGate = new object ();
    private static readonly Dictionary<string, long> LastSpeakLocal = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private static readonly Regex UrlLike = new Regex(@"(https?:\/\/|www\.)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] LoggerSecrets =
    {
        "azure_key",
        "azure_key2"
    };
    public bool Execute()
    {
        return DoSpeak();
    }

    public bool TtsSpeak() => DoSpeak();
    public bool TtsUnlock() => DoUnlock();
    public bool TtsCommand() => DoCommand();
#region Settings
    private sealed class VoiceInfo
    {
        public string Alias;
        public string Display;
        public string AzureId;
        public string[] Styles;
    }

    private sealed class Settings
    {
        public string AzureKey { get; set; } = "";
        public string AzureKey2 { get; set; } = "";
        public string Region { get; set; } = "northeurope";
        public int Volume { get; set; } = 50;
        public string SpeakRewardId { get; set; } = "";
        public string UnlockRewardId { get; set; } = "";
        public int CooldownSeconds { get; set; } = 15;
        public int MaxChars { get; set; } = 250;
        public bool BlockUrls { get; set; } = true;
        public bool SpamCharRepeat { get; set; }
        public int SpamCharRepeatMax { get; set; } = 8;
        public bool SpamWordRepeat { get; set; }
        public int SpamWordRepeatMax { get; set; } = 5;
        public bool SpamPhrase { get; set; }
        public int SpamPhraseMaxWords { get; set; } = 6;
        public int SpamPhraseMinRepeats { get; set; } = 5;
        public bool SpamUnique { get; set; }
        public int SpamUniqueMinWords { get; set; } = 8;
        public double SpamUniqueMinRatio { get; set; } = 0.35;
        public bool SpamBlockedWords { get; set; }
        public string[] BlockedWords { get; set; } = Array.Empty<string>();
        public string DefaultVoice { get; set; } = "en";
        public string[] Voices { get; set; } = Array.Empty<string>();
        public List<VoiceInfo> Catalog { get; set; } = new List<VoiceInfo>();
        public string MsgHelp { get; set; } = "@{user} Redeem TTS Message to speak (default English). Prefix a voice you own: {prefix}hello. Unlock voices with Unlock TTS Voice. !tts voices | !tts set <alias>";
        public string MsgCooldown { get; set; } = "@{user} TTS is on cooldown ({seconds}s left).";
        public string MsgPaused { get; set; } = "@{user} TTS is paused right now.";
        public string MsgUnpaid { get; set; } = "@{user} You don't own {alias}. Unlock it with Unlock TTS Voice.";
        public string MsgUnknownPrefix { get; set; } = "@{user} Unknown voice '{prefix}'. Unlock names: {voices}.";
        public string MsgUnknownStyle { get; set; } = "@{user} {alias} has no style '{style}'.";
        public string MsgEmpty { get; set; } = "@{user} Type a message after the prefix.";
        public string MsgTooLong { get; set; } = "@{user} TTS is too long (max {max} characters).";
        public string MsgUrl { get; set; } = "@{user} Links are not allowed in TTS.";
        public string MsgSpam { get; set; } = "@{user} That TTS looks like spam.";
        public string MsgUnlockOk { get; set; } = "@{user} unlocked {alias}. TTS prefix: {prefix}hello. Sticky: !tts set {alias}";
        public string MsgUnlockOwned { get; set; } = "@{user} You already own {alias}.";
        public string MsgUnlockUnknown { get; set; } = "@{user} Unknown voice. Try: {voices}";
        public string MsgSetOk { get; set; } = "@{user} Default TTS voice is now {alias}.";
        public string MsgSetNeedOwn { get; set; } = "@{user} Unlock {alias} first, or use !tts set default.";
        public string MsgNeedMod { get; set; } = "@{user} Mods only.";
        public string MsgOff { get; set; } = "TTS rewards paused.";
        public string MsgOn { get; set; } = "TTS rewards are live.";
        public string MsgSpeakRewardOnly { get; set; } = "@{user} Speak with the TTS Message reward, not !tts. Try !tts for help.";
        public string MsgSynthFail { get; set; } = "@{user} TTS failed to play. Try again in a bit.";
    }

    private sealed class PauseState
    {
        public bool Paused { get; set; }
    }

    private sealed class PrefixParse
    {
        public VoiceInfo Voice;
        public string Style;
        public string Message;
        public string RawPrefix;
        public string ErrorKey;
    }

    private Settings LoadSettings()
    {
        var s = Fc.LoadSettings<Settings>(CPH, Title, x =>
        {
            if (string.IsNullOrWhiteSpace(x.Region))
                x.Region = "northeurope";
            else
                x.Region = x.Region.Trim();
            x.Volume = Clamp(x.Volume, 0, 100);
            x.CooldownSeconds = Clamp(x.CooldownSeconds, 0, 3600);
            x.MaxChars = Clamp(x.MaxChars, 1, 2000);
            x.SpamCharRepeatMax = Clamp(x.SpamCharRepeatMax, 2, 40);
            x.SpamWordRepeatMax = Clamp(x.SpamWordRepeatMax, 2, 50);
            x.SpamPhraseMaxWords = Clamp(x.SpamPhraseMaxWords, 1, 20);
            x.SpamPhraseMinRepeats = Clamp(x.SpamPhraseMinRepeats, 2, 20);
            x.SpamUniqueMinWords = Clamp(x.SpamUniqueMinWords, 3, 100);
            x.SpamUniqueMinRatio = ClampDouble(x.SpamUniqueMinRatio, 0.05, 1.0);
            if (x.BlockedWords == null)
                x.BlockedWords = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(x.DefaultVoice))
                x.DefaultVoice = "";
            else
                x.DefaultVoice = x.DefaultVoice.Trim();
        });
        var aliases = s.Voices ?? Array.Empty<string>();
        s.Catalog = new List<VoiceInfo>();
        foreach (var raw in aliases)
        {
            string alias = (raw ?? "").Trim();
            if (alias.Length == 0)
                continue;
            string display = Fc.GetSetting(CPH, Title, alias + "_display", alias);
            string azure = Fc.GetSetting(CPH, Title, alias + "_azure", "");
            string stylesRaw = Fc.GetSetting(CPH, Title, alias + "_styles", "");
            s.Catalog.Add(new VoiceInfo { Alias = alias, Display = string.IsNullOrWhiteSpace(display) ? alias : display.Trim(), AzureId = (azure ?? "").Trim(), Styles = ParseStyles(stylesRaw), });
        }

        if (s.Catalog.Count == 0)
        {
            s.Catalog.Add(new VoiceInfo { Alias = "en", Display = "English", AzureId = FallbackAzureVoice, Styles = Array.Empty<string>(), });
            s.DefaultVoice = "en";
        }
        else if (FindVoice(s, s.DefaultVoice) == null)
        {
            s.DefaultVoice = s.Catalog[0].Alias;
        }

        var def = FindVoice(s, s.DefaultVoice);
        if (def != null && string.IsNullOrWhiteSpace(def.AzureId))
            def.AzureId = FallbackAzureVoice;
        s.Volume = Clamp(Fc.GetSetting(CPH, Title, "volume", s.Volume), 0, 100);
        return s;
    }

    private static string[] ParseStyles(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();
        return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
    }

#endregion
#region Speak
    private bool DoSpeak()
    {
        var log = Fc.Logger(CPH, Title, Version, LoggerSecrets);
        if (!EnsureReady(log, isRedemption: true, needAzure: true))
            return false;
        var settings = LoadSettings();
        var ev = Fc.CaptureEvent(CPH);
        string user = MentionName(ev);
        string userId = ResolveUserId(ev);
        string userName = CooldownUserName(ev, user);
        TryGetRedemptionIds(out var rewardId, out var redemptionId);
        if (!IsSpeakRedemption(settings, rewardId))
        {
            log.Info("Speak skipped; this redemption is the unlock reward.");
            return true;
        }

        string input = GetRewardInput(ev);
        log.Info("Speak user=" + user + " chars=" + (input ?? "").Length);
        if (IsPaused())
            return Refund(rewardId, redemptionId, settings.MsgPaused, user, settings, log, "paused");
        if (settings.CooldownSeconds > 0 && string.IsNullOrWhiteSpace(CooldownKey(userId, userName)))
            log.Warn("Cooldown skipped; no Twitch user id or name on this redemption.");
        int wait = CooldownRemainingSeconds(userId, userName, settings.CooldownSeconds);
        if (wait > 0)
        {
            log.Info("Cooldown user=" + user + " wait=" + wait + "s");
            return Refund(rewardId, redemptionId, settings.MsgCooldown, user, settings, log, "cooldown", new Dictionary<string, string> { ["seconds"] = wait.ToString(CultureInfo.InvariantCulture) });
        }

        var parsed = ParsePrefix(input, userId, settings);
        if (parsed.ErrorKey != null)
        {
            string template = TemplateFor(settings, parsed.ErrorKey);
            var extra = VarsFromParse(parsed, settings);
            return Refund(rewardId, redemptionId, template, user, settings, log, parsed.ErrorKey, extra);
        }

        string spam = CheckMessage(parsed.Message, settings);
        if (spam != null)
        {
            string template = TemplateFor(settings, spam);
            return Refund(rewardId, redemptionId, template, user, settings, log, spam, VarsFromParse(parsed, settings));
        }

        var keys = AzureKeys(settings);
        if (keys.Count == 0 || string.IsNullOrWhiteSpace(parsed.Voice.AzureId))
        {
            FailSetup("TTS is missing an Azure key or a voice ID. Open TTS Settings, paste the Speech key, check each voice's Azure ID, and Save.", "@" + user + " TTS isn't configured yet. Points refunded.", log, true, rewardId, redemptionId);
            return false;
        }

        bool ok = SynthesizeAndPlay(settings, keys, parsed.Voice.AzureId, parsed.Style, parsed.Message, log);
        if (!ok)
            return Refund(rewardId, redemptionId, settings.MsgSynthFail, user, settings, log, "azure");
        Fulfill(rewardId, redemptionId);
        SetLastSpeakMs(userId, userName, UnixMs());
        log.Info($"{user} TTS alias={parsed.Voice.Alias} style={parsed.Style ?? "-"} chars={parsed.Message.Length}");
        return true;
    }

#endregion
#region Unlock
    private bool DoUnlock()
    {
        var log = Fc.Logger(CPH, Title, Version, LoggerSecrets);
        if (!EnsureReady(log, isRedemption: true, needAzure: false))
            return false;
        var settings = LoadSettings();
        var ev = Fc.CaptureEvent(CPH);
        string user = MentionName(ev);
        string userId = ev.UserId;
        TryGetRedemptionIds(out var rewardId, out var redemptionId);
        if (!IsUnlockRedemption(settings, rewardId))
        {
            log.Info("Unlock skipped; this redemption is not the unlock reward.");
            return true;
        }

        string input = GetRewardInput(ev);
        if (IsPaused())
            return Refund(rewardId, redemptionId, settings.MsgPaused, user, settings, log, "paused");
        var voice = FindVoice(settings, input);
        if (voice == null)
        {
            return Refund(rewardId, redemptionId, settings.MsgUnlockUnknown, user, settings, log, "unlock-unknown");
        }

        var owned = GetOwned(userId);
        if (Owns(owned, voice.Alias))
        {
            return Refund(rewardId, redemptionId, settings.MsgUnlockOwned, user, settings, log, "unlock-owned", VarsForVoice(voice, settings));
        }

        owned.Add(voice.Alias);
        SetOwned(userId, owned);
        Fulfill(rewardId, redemptionId);
        Chat(settings.MsgUnlockOk, user, ev, settings, VarsForVoice(voice, settings));
        log.Info($"{user} unlocked {voice.Alias}");
        return true;
    }

#endregion
#region Command
    private bool DoCommand()
    {
        var log = Fc.Logger(CPH, Title, Version, LoggerSecrets);
        if (!EnsureReady(log, isRedemption: false, needAzure: false))
            return false;
        var settings = LoadSettings();
        var ev = Fc.CaptureEvent(CPH);
        string user = MentionName(ev);
        string leftover = (ev.RawInput ?? "").Trim();
        if (leftover.Length == 0)
        {
            Chat(settings.MsgHelp, user, ev, settings, HelpVars(settings));
            return true;
        }

        var tokens = leftover.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        string head = tokens[0];
        if (Eq(head, "help"))
        {
            Chat(settings.MsgHelp, user, ev, settings, HelpVars(settings));
            return true;
        }

        if (Eq(head, "voices") || Eq(head, "inventory"))
        {
            ReplyVoices(user, ev.UserId, ev, settings);
            return true;
        }

        if (Eq(head, "off") || Eq(head, "pause"))
        {
            if (!IsModOrBroadcaster(ev))
            {
                Chat(settings.MsgNeedMod, user, ev, settings, null);
                return true;
            }

            SetPaused(true, settings, log);
            Chat(settings.MsgOff, user, ev, settings, null);
            return true;
        }

        if (Eq(head, "on") || Eq(head, "unpause"))
        {
            if (!IsModOrBroadcaster(ev))
            {
                Chat(settings.MsgNeedMod, user, ev, settings, null);
                return true;
            }

            SetPaused(false, settings, log);
            Chat(settings.MsgOn, user, ev, settings, null);
            return true;
        }

        if (Eq(head, "set") || Eq(head, "voice"))
        {
            int i = 1;
            if (i < tokens.Length && Eq(head, "set") && Eq(tokens[i], "voice"))
                i++;
            string want = i < tokens.Length ? string.Join(" ", tokens.Skip(i)) : "";
            HandleSetVoice(user, ev.UserId, ev, settings, want);
            return true;
        }

        Chat(settings.MsgSpeakRewardOnly, user, ev, settings, null);
        return true;
    }

    private void HandleSetVoice(string user, string userId, EventContext ev, Settings settings, string want)
    {
        if (string.IsNullOrWhiteSpace(want) || Eq(want, "default") || Eq(want, settings.DefaultVoice))
        {
            CPH.SetTwitchUserVarById(userId, StickyVar, "", true);
            var def = FindVoice(settings, settings.DefaultVoice);
            Chat(settings.MsgSetOk, user, ev, settings, VarsForVoice(def, settings));
            return;
        }

        var voice = FindVoice(settings, want);
        if (voice == null)
        {
            Chat(settings.MsgUnlockUnknown, user, ev, settings, null);
            return;
        }

        if (Eq(voice.Alias, settings.DefaultVoice))
        {
            CPH.SetTwitchUserVarById(userId, StickyVar, "", true);
            Chat(settings.MsgSetOk, user, ev, settings, VarsForVoice(voice, settings));
            return;
        }

        if (!Owns(GetOwned(userId), voice.Alias))
        {
            Chat(settings.MsgSetNeedOwn, user, ev, settings, VarsForVoice(voice, settings));
            return;
        }

        CPH.SetTwitchUserVarById(userId, StickyVar, voice.Alias, true);
        Chat(settings.MsgSetOk, user, ev, settings, VarsForVoice(voice, settings));
    }

    private void ReplyVoices(string user, string userId, EventContext ev, Settings settings)
    {
        var owned = GetOwned(userId);
        string sticky = GetStickyAlias(userId, settings);
        var ownedBits = settings.Catalog.Where(v => Owns(owned, v.Alias) || Eq(v.Alias, settings.DefaultVoice)).Select(v =>
        {
            string mark = Eq(v.Alias, sticky) ? "*" : "";
            bool isFreeDefault = Eq(v.Alias, settings.DefaultVoice) && !Owns(owned, v.Alias);
            var bits = new List<string>();
            if (isFreeDefault)
                bits.Add("free");
            if (v.Styles.Length > 0)
                bits.Add(string.Join(", ", v.Styles));
            return bits.Count == 0 ? $"{v.Alias}{mark}" : $"{v.Alias}{mark} ({string.Join("; ", bits)})";
        });
        string buyable = string.Join(", ", settings.Catalog.Select(v => $"{v.Display}={v.Alias}"));
        string ownedText = string.Join(" | ", ownedBits);
        if (string.IsNullOrWhiteSpace(ownedText))
            ownedText = "(none)";
        var sample = settings.Catalog.FirstOrDefault(v => !Eq(v.Alias, settings.DefaultVoice)) ?? settings.Catalog[0];
        string prefixHint = sample.Alias + " hello";
        var styled = settings.Catalog.FirstOrDefault(v => v.Styles.Length > 0);
        if (styled != null)
            prefixHint += "; " + styled.Alias + "::" + styled.Styles[0] + " hello";
        string line = $"@{user} Voices: {ownedText}. Buy: {buyable}. Sticky *: !tts set <alias>. Prefix: {prefixHint}";
        Reply(line, ev);
    }

#endregion
#region Prefix
    private PrefixParse ParsePrefix(string input, string userId, Settings settings)
    {
        var result = new PrefixParse();
        string text = (input ?? "").Trim();
        if (text.Length == 0)
        {
            result.ErrorKey = "empty";
            return result;
        }

        int space = text.IndexOf(' ');
        string token = space < 0 ? text : text.Substring(0, space);
        string rest = space < 0 ? "" : text.Substring(space + 1).Trim();
        int styleSep = token.IndexOf("::", StringComparison.Ordinal);
        if (styleSep >= 0)
        {
            string left = token.Substring(0, styleSep).Trim();
            string stylePart = token.Substring(styleSep + 2).Trim();
            result.RawPrefix = token;
            result.Message = rest;
            result.Voice = left.Length == 0 ? ResolveStickyOrDefault(userId, settings) : FindVoiceByAlias(settings, left);
            if (result.Voice == null)
            {
                result.ErrorKey = "unknown_prefix";
                return result;
            }

            result.Style = stylePart.Length == 0 ? null : stylePart;
            return FinishPrefix(result, userId, settings);
        }

        string aliasToken = token.TrimEnd(':');
        bool trailingColons = aliasToken.Length > 0 && aliasToken.Length < token.Length;
        var prefixed = FindVoiceByAlias(settings, trailingColons ? aliasToken : token);
        if (prefixed != null)
        {
            result.Voice = prefixed;
            result.RawPrefix = token;
            result.Message = rest;
            return FinishPrefix(result, userId, settings);
        }

        result.Voice = ResolveStickyOrDefault(userId, settings);
        result.Message = text;
        if (!CanUseVoice(userId, result.Voice, null, settings))
        {
            result.ErrorKey = "unpaid";
            result.RawPrefix = result.Voice.Alias;
        }

        return result;
    }

    private PrefixParse FinishPrefix(PrefixParse result, string userId, Settings settings)
    {
        if (!CanUseVoice(userId, result.Voice, result.Style, settings))
        {
            result.ErrorKey = "unpaid";
            return result;
        }

        if (!string.IsNullOrEmpty(result.Style) && !HasStyle(result.Voice, result.Style))
        {
            result.ErrorKey = "unknown_style";
            return result;
        }

        if (string.IsNullOrWhiteSpace(result.Message))
            result.ErrorKey = "empty";
        return result;
    }

    private VoiceInfo ResolveStickyOrDefault(string userId, Settings settings)
    {
        string sticky = GetStickyAlias(userId, settings);
        return FindVoice(settings, sticky) ?? FindVoice(settings, settings.DefaultVoice) ?? settings.Catalog[0];
    }

    private bool CanUseVoice(string userId, VoiceInfo voice, string style, Settings settings)
    {
        if (voice == null)
            return false;
        bool isDefault = Eq(voice.Alias, settings.DefaultVoice);
        bool needsOwn = !isDefault || !string.IsNullOrEmpty(style);
        if (!needsOwn)
            return true;
        return Owns(GetOwned(userId), voice.Alias);
    }

    private static bool HasStyle(VoiceInfo voice, string style)
    {
        if (voice == null || string.IsNullOrWhiteSpace(style))
            return false;
        return voice.Styles.Any(s => Eq(s, style));
    }

#endregion
#region Azure
    private bool SynthesizeAndPlay(Settings settings, List<string> keys, string azureVoice, string style, string text, ExtensionLogger log)
    {
        string path = null;
        try
        {
            path = Path.Combine(Path.GetTempPath(), "sb-tts-" + Guid.NewGuid().ToString("N") + ".mp3");
            string url = "https://" + settings.Region.Trim() + ".tts.speech.microsoft.com/cognitiveservices/v1";
            log.Info("Azure voice=" + azureVoice + " style=" + (style ?? "-") + " region=" + settings.Region);
            bool downloaded = false;
            foreach (string ssml in BuildSsmlAttempts(azureVoice, style, text))
            {
                foreach (string key in keys)
                {
                    if (TryPostAzure(url, key, ssml, path, log))
                    {
                        downloaded = true;
                        break;
                    }
                }

                if (downloaded)
                    break;
            }

            if (!downloaded)
                return false;
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0)
            {
                log.Error("Azure returned empty audio.");
                return false;
            }

            float vol = settings.Volume / 100f;
            CPH.PlaySound(path, vol, true);
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex.GetType().Name + ": " + ex.Message);
            return false;
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static IEnumerable<string> BuildSsmlAttempts(string azureVoice, string style, string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string voice in VoiceNameFallbacks(azureVoice))
        {
            string locale = LocaleFromVoice(voice);
            foreach (string ssml in new[]
            {
                BuildSsml(voice, locale, style, text, false),
                BuildSsml(voice, locale, style, text, true),
                BuildSsml(voice, locale, null, text, true),
            }

            )
            {
                if (seen.Add(ssml))
                    yield return ssml;
            }
        }
    }

    private static IEnumerable<string> VoiceNameFallbacks(string azureVoice)
    {
        yield return azureVoice;
        if (string.IsNullOrWhiteSpace(azureVoice))
            yield break;
        if (azureVoice.EndsWith("Neural", StringComparison.OrdinalIgnoreCase) && azureVoice.IndexOf("Multilingual", StringComparison.OrdinalIgnoreCase) < 0)
        {
            int cut = azureVoice.Length - "Neural".Length;
            yield return azureVoice.Substring(0, cut) + "MultilingualNeural";
        }
    }

    private static string BuildSsml(string azureVoice, string locale, string style, string text, bool wrapEnglish)
    {
        string inner = XmlEscape(text);
        if (wrapEnglish && LooksMostlyLatin(text) && !locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            inner = "<lang xml:lang=\"en-US\">" + inner + "</lang>";
        bool useStyle = !string.IsNullOrEmpty(style);
        if (useStyle)
            inner = "<mstts:express-as style=\"" + XmlEscape(style) + "\">" + inner + "</mstts:express-as>";
        string ns = "xmlns=\"http://www.w3.org/2001/10/synthesis\"";
        if (useStyle)
            ns += " xmlns:mstts=\"https://www.w3.org/2001/mstts\"";
        return "<speak version=\"1.0\" " + ns + " xml:lang=\"" + locale + "\">" + "<voice name=\"" + XmlEscape(azureVoice) + "\">" + inner + "</voice></speak>";
    }

    private static bool LooksMostlyLatin(string text)
    {
        int letters = 0;
        int latin = 0;
        foreach (char c in text ?? "")
        {
            if (!char.IsLetter(c))
                continue;
            letters++;
            if (c <= 0x024F)
                latin++;
        }

        return letters == 0 || latin * 2 >= letters;
    }

    private bool TryPostAzure(string url, string key, string ssml, string path, ExtensionLogger log)
    {
        try
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", key);
                req.Headers.TryAddWithoutValidation("User-Agent", "StreamerBotTts");
                req.Headers.TryAddWithoutValidation("X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");
                byte[] bytes = Encoding.UTF8.GetBytes(ssml);
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/ssml+xml");
                req.Content = content;
                using (var resp = Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        string err = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        log.Error("Azure HTTP " + (int)resp.StatusCode + " voice-ssml failed: " + Trim(err, 500));
                        return false;
                    }

                    using (var src = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var dst = File.Create(path))
                    {
                        src.CopyTo(dst);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            log.Error("Azure post " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static string LocaleFromVoice(string voice)
    {
        if (string.IsNullOrEmpty(voice))
            return "en-US";
        int dash = voice.IndexOf('-');
        if (dash < 0)
            return "en-US";
        int dash2 = voice.IndexOf('-', dash + 1);
        return dash2 > 0 ? voice.Substring(0, dash2) : "en-US";
    }

    private static string XmlEscape(string s)
    {
        return SecurityElement.Escape(s ?? "") ?? "";
    }

    private static string Trim(string s, int max)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        // ignore temp cleanup
        }
    }

    private static List<string> AzureKeys(Settings s)
    {
        var keys = new List<string>();
        void Add(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("PASTE_", StringComparison.OrdinalIgnoreCase))
                return;
            string k = raw.Trim();
            if (!keys.Contains(k, StringComparer.Ordinal))
                keys.Add(k);
        }

        Add(s.AzureKey);
        Add(s.AzureKey2);
        return keys;
    }

#endregion
#region Cooldown pause ownership
    private bool IsPaused()
    {
        var state = Fc.LoadData<PauseState>(CPH, Title);
        return state != null && state.Paused;
    }

    private void SetPaused(bool paused, Settings settings, ExtensionLogger log)
    {
        Fc.SaveData(CPH, Title, new PauseState { Paused = paused });
        foreach (var id in new[]
        {
            settings.SpeakRewardId,
            settings.UnlockRewardId
        }

        )
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            try
            {
                if (paused)
                    CPH.PauseReward(id);
                else
                    CPH.UnPauseReward(id);
            }
            catch (Exception ex)
            {
                log.Warn("PauseReward " + id + ": " + ex.Message);
            }
        }
    }

    private int CooldownRemainingSeconds(string userId, string userName, int cooldownSeconds)
    {
        if (cooldownSeconds <= 0)
            return 0;
        string key = CooldownKey(userId, userName);
        if (string.IsNullOrWhiteSpace(key))
            return 0;
        long last = 0;
        lock (CooldownGate)
        {
            LastSpeakLocal.TryGetValue(key, out last);
        }

        long stored = ReadLastSpeakMs(userId, userName);
        if (stored > last)
            last = stored;
        if (last <= 0)
            return 0;
        long elapsed = UnixMs() - last;
        long need = cooldownSeconds * 1000L;
        if (elapsed >= need)
            return 0;
        long left = need - elapsed;
        return (int)Math.Ceiling(left / 1000.0);
    }

    private void SetLastSpeakMs(string userId, string userName, long ms)
    {
        string key = CooldownKey(userId, userName);
        if (string.IsNullOrWhiteSpace(key) || ms <= 0)
            return;
        lock (CooldownGate)
        {
            LastSpeakLocal[key] = ms;
        }

        WriteLastSpeakMsVar(userId, userName, ms);
    }

    private void WriteLastSpeakMsVar(string userId, string userName, long ms)
    {
        string value = ms.ToString(CultureInfo.InvariantCulture);
        try
        {
            if (!string.IsNullOrWhiteSpace(userId))
                CPH.SetTwitchUserVarById(userId, LastMsVar, value, false);
            else if (!string.IsNullOrWhiteSpace(userName))
                CPH.SetTwitchUserVar(userName, LastMsVar, value, false);
        }
        catch
        {
        }
    }

    private long ReadLastSpeakMs(string userId, string userName)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            long fromId = ReadUserVarMsById(userId);
            if (fromId > 0)
                return fromId;
        }

        if (!string.IsNullOrWhiteSpace(userName))
            return ReadUserVarMsByName(userName);
        return 0;
    }

    private long ReadUserVarMsById(string userId)
    {
        try
        {
            long n = CPH.GetTwitchUserVarById<long>(userId, LastMsVar, false);
            if (n > 0)
                return n;
        }
        catch
        {
        }

        try
        {
            return CoerceUnixMs(CPH.GetTwitchUserVarById<string>(userId, LastMsVar, false));
        }
        catch
        {
            return 0;
        }
    }

    private long ReadUserVarMsByName(string userName)
    {
        try
        {
            long n = CPH.GetTwitchUserVar<long>(userName, LastMsVar, false);
            if (n > 0)
                return n;
        }
        catch
        {
        }

        try
        {
            return CoerceUnixMs(CPH.GetTwitchUserVar<string>(userName, LastMsVar, false));
        }
        catch
        {
            return 0;
        }
    }

    private static long CoerceUnixMs(object raw)
    {
        if (raw == null)
            return 0;
        if (raw is long l)
            return l > 0 ? l : 0;
        if (raw is int i)
            return i > 0 ? i : 0;
        if (raw is double d && d > 0 && d <= long.MaxValue)
            return (long)d;
        string s = Convert.ToString(raw, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(s))
            return 0;
        if (long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0)
            return parsed;
        return 0;
    }

    private static string CooldownKey(string userId, string userName)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            return "id:" + userId.Trim();
        if (!string.IsNullOrWhiteSpace(userName))
            return "name:" + userName.Trim();
        return "";
    }

    private string ResolveUserId(EventContext ev)
    {
        if (!string.IsNullOrWhiteSpace(ev.UserId))
            return ev.UserId.Trim();
        return FirstArg("userId", "UserId", "userID", "fromId");
    }

    private static string CooldownUserName(EventContext ev, string mention)
    {
        if (!string.IsNullOrWhiteSpace(ev.UserName))
            return ev.UserName.Trim();
        if (!string.IsNullOrWhiteSpace(ev.User))
            return ev.User.Trim();
        return mention ?? "";
    }

    private List<string> GetOwned(string userId)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(userId))
            return list;
        try
        {
            string raw = CPH.GetTwitchUserVarById<string>(userId, OwnedVar, true);
            if (string.IsNullOrWhiteSpace(raw))
                return list;
            var arr = JArray.Parse(raw);
            foreach (var t in arr)
            {
                string a = (t?.ToString() ?? "").Trim();
                if (a.Length > 0 && !list.Contains(a, StringComparer.OrdinalIgnoreCase))
                    list.Add(a);
            }
        }
        catch
        {
            return list;
        }

        return list;
    }

    private void SetOwned(string userId, List<string> owned)
    {
        var arr = new JArray(owned.Select(a => a));
        CPH.SetTwitchUserVarById(userId, OwnedVar, arr.ToString(Newtonsoft.Json.Formatting.None), true);
    }

    private string GetStickyAlias(string userId, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return settings.DefaultVoice;
        string raw = CPH.GetTwitchUserVarById<string>(userId, StickyVar, true);
        if (string.IsNullOrWhiteSpace(raw))
            return settings.DefaultVoice;
        var v = FindVoice(settings, raw.Trim());
        if (v == null)
            return settings.DefaultVoice;
        if (!Eq(v.Alias, settings.DefaultVoice) && !Owns(GetOwned(userId), v.Alias))
            return settings.DefaultVoice;
        return v.Alias;
    }

    private static bool Owns(List<string> owned, string alias)
    {
        if (owned == null || string.IsNullOrEmpty(alias))
            return false;
        return owned.Any(a => Eq(a, alias));
    }

#endregion
#region Setup notice
    private bool EnsureReady(ExtensionLogger log, bool isRedemption, bool needAzure)
    {
        var ev = Fc.CaptureEvent(CPH);
        string user = MentionName(ev);
        TryGetRedemptionIds(out var rewardId, out var redemptionId);
        string chatFail = isRedemption ? (string.IsNullOrWhiteSpace(user) ? "TTS isn't set up yet. Points refunded." : "@" + user + " TTS isn't set up yet. Points refunded.") : (string.IsNullOrWhiteSpace(user) ? "TTS isn't set up yet." : "@" + user + " TTS isn't set up yet. The streamer still needs to open TTS Settings.");
        if (!Fc.HasSavedSettings(CPH, Title))
        {
            FailSetup("TTS is not set up. Run the TTS Settings action, paste your Azure Speech key, pick the two rewards, and click Save.", chatFail, log, isRedemption, rewardId, redemptionId);
            return false;
        }

        if (!needAzure || AzureKeys(LoadSettings()).Count > 0)
            return true;
        FailSetup("TTS has no Azure Speech key. Open TTS Settings, paste the key, and Save.", isRedemption ? (string.IsNullOrWhiteSpace(user) ? "TTS isn't configured yet. Points refunded." : "@" + user + " TTS isn't configured yet. Points refunded.") : chatFail, log, isRedemption, rewardId, redemptionId);
        return false;
    }

    private void FailSetup(string streamerMessage, string chatMessage, ExtensionLogger log, bool popup, string rewardId, string redemptionId)
    {
        log.Warn(streamerMessage);
        NotifyStreamer(streamerMessage, popup);
        if (!string.IsNullOrWhiteSpace(rewardId) && !string.IsNullOrWhiteSpace(redemptionId))
        {
            try
            {
                CPH.TwitchRedemptionCancel(rewardId, redemptionId);
            }
            catch (Exception ex)
            {
                log.Warn("Cancel failed: " + ex.Message);
            }
        }

        if (!string.IsNullOrWhiteSpace(chatMessage))
            CPH.SendMessage(chatMessage);
    }

    private void NotifyStreamer(string message, bool popup)
    {
        try
        {
            CPH.ShowToastNotification("TTS", message);
        }
        catch
        {
            try
            {
                CPH.ShowToastNotification("TTS", message, "TTS", "");
            }
            catch
            {
            // toast optional
            }
        }

        if (!popup)
            return;
        try
        {
            var t = new Thread(() =>
            {
                try
                {
                    MessageBox.Show(message, "TTS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch
                {
                // ignore STA/dialog failures
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
        catch
        {
        // ignore
        }
    }

#endregion
#region Redemption chat helpers
    private bool Refund(string rewardId, string redemptionId, string template, string user, Settings settings, ExtensionLogger log, string reason, Dictionary<string, string> extra = null)
    {
        if (!string.IsNullOrWhiteSpace(rewardId) && !string.IsNullOrWhiteSpace(redemptionId))
        {
            try
            {
                CPH.TwitchRedemptionCancel(rewardId, redemptionId);
            }
            catch (Exception ex)
            {
                log.Warn("Cancel failed: " + ex.Message);
            }
        }

        var ev = Fc.CaptureEvent(CPH);
        Chat(template, user, ev, settings, extra);
        log.Info("Refund " + reason + " user=" + user);
        return false;
    }

    private void Fulfill(string rewardId, string redemptionId)
    {
        if (string.IsNullOrWhiteSpace(rewardId) || string.IsNullOrWhiteSpace(redemptionId))
            return;
        try
        {
            CPH.TwitchRedemptionFulfill(rewardId, redemptionId);
        }
        catch
        {
        // already fulfilled or skip-queue
        }
    }

    private void TryGetRedemptionIds(out string rewardId, out string redemptionId)
    {
        rewardId = FirstArg("rewardId", "RewardId", "rewardID");
        redemptionId = FirstArg("redemptionId", "RedemptionId", "redemptionID");
    }

    private string FirstArg(params string[] names)
    {
        foreach (var name in names)
        {
            if (CPH.TryGetArg(name, out string s) && !string.IsNullOrWhiteSpace(s))
                return s.Trim();
            if (CPH.TryGetArg(name, out object o) && o != null)
            {
                string t = Convert.ToString(o);
                if (!string.IsNullOrWhiteSpace(t))
                    return t.Trim();
            }
        }

        return "";
    }

    private bool IsSpeakRedemption(Settings settings, string rewardId)
    {
        if (MatchesId(settings.SpeakRewardId, rewardId))
            return true;
        if (MatchesId(settings.UnlockRewardId, rewardId))
            return false;
        string title = RewardTitle();
        if (TitleLooksLikeUnlock(title))
            return false;
        return true;
    }

    private bool IsUnlockRedemption(Settings settings, string rewardId)
    {
        if (MatchesId(settings.UnlockRewardId, rewardId))
            return true;
        if (MatchesId(settings.SpeakRewardId, rewardId))
            return false;
        return TitleLooksLikeUnlock(RewardTitle());
    }

    private static bool TitleLooksLikeUnlock(string title)
    {
        return !string.IsNullOrWhiteSpace(title) && title.IndexOf("unlock", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MatchesId(string configured, string incoming)
    {
        return !string.IsNullOrWhiteSpace(configured) && Eq(configured.Trim(), incoming);
    }

    private string RewardTitle()
    {
        foreach (var key in new[]
        {
            "rewardName",
            "rewardTitle",
            "reward",
            "triggerName"
        }

        )
        {
            if (CPH.TryGetArg(key, out string v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return "";
    }

    private string GetRewardInput(EventContext ev)
    {
        if (!string.IsNullOrWhiteSpace(ev.RawInput))
            return ev.RawInput.Trim();
        foreach (var key in new[]
        {
            "userInput",
            "user_input",
            "input",
            "rawInput"
        }

        )
        {
            if (CPH.TryGetArg(key, out string v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ev.Message))
            return ev.Message.Trim();
        return "";
    }

    private static readonly char[] WordSeparators =
    {
        ' '
    };
    private string CheckMessage(string message, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "empty";
        if (message.Length > settings.MaxChars)
            return "too_long";
        if (settings.BlockUrls && UrlLike.IsMatch(message))
            return "url";
        return CheckAntiSpam(message, settings);
    }

    private static string CheckAntiSpam(string message, Settings settings)
    {
        bool needWords = settings.SpamBlockedWords || settings.SpamWordRepeat || settings.SpamPhrase || settings.SpamUnique;
        if (!settings.SpamCharRepeat && !needWords)
            return null;
        string normalized = NormalizeMessage(message);
        if (string.IsNullOrEmpty(normalized))
            return null;
        string[] words = needWords ? Tokenize(normalized) : Array.Empty<string>();
        if (settings.SpamBlockedWords && HasBlockedTerm(words, settings.BlockedWords))
            return "spam_blocked";
        if (settings.SpamCharRepeat && HasTooManyRepeatedChars(normalized, settings.SpamCharRepeatMax))
            return "spam_char";
        if (settings.SpamWordRepeat && HasRepeatedWord(words, settings.SpamWordRepeatMax))
            return "spam_word";
        if (settings.SpamPhrase && HasRepetitivePhrase(words, settings.SpamPhraseMaxWords, settings.SpamPhraseMinRepeats))
            return "spam_phrase";
        if (settings.SpamUnique && words.Length >= settings.SpamUniqueMinWords)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var w in words)
                seen.Add(w);
            if ((double)seen.Count / words.Length < settings.SpamUniqueMinRatio)
                return "spam_unique";
        }

        return null;
    }

    private static string NormalizeMessage(string message)
    {
        var trimmed = (message ?? "").Trim();
        var sb = new StringBuilder(trimmed.Length);
        bool prevSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
            }
            else
            {
                sb.Append(char.ToLowerInvariant(ch));
                prevSpace = false;
            }
        }

        return sb.ToString();
    }

    private static string[] Tokenize(string normalized)
    {
        if (string.IsNullOrEmpty(normalized))
            return Array.Empty<string>();
        var parts = normalized.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        int n = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            string t = NormalizeToken(parts[i]);
            if (t.Length == 0)
                continue;
            parts[n++] = t;
        }

        if (n == parts.Length)
            return parts;
        var trimmed = new string[n];
        Array.Copy(parts, trimmed, n);
        return trimmed;
    }

    private static string NormalizeToken(string word)
    {
        if (string.IsNullOrEmpty(word))
            return "";
        int start = 0;
        int end = word.Length - 1;
        while (start <= end && char.IsPunctuation(word[start]))
            start++;
        while (end >= start && char.IsPunctuation(word[end]))
            end--;
        if (start > end)
            return "";
        return word.Substring(start, end - start + 1);
    }

    private static bool HasBlockedTerm(string[] words, string[] blocked)
    {
        if (blocked == null || blocked.Length == 0 || words == null || words.Length == 0)
            return false;
        string padded = null;
        for (int b = 0; b < blocked.Length; b++)
        {
            string term = string.Join(" ", Tokenize(NormalizeMessage(blocked[b] ?? "")));
            if (term.Length == 0)
                continue;
            if (term.IndexOf(' ') >= 0)
            {
                if (padded == null)
                    padded = " " + string.Join(" ", words) + " ";
                if (padded.IndexOf(" " + term + " ", StringComparison.Ordinal) >= 0)
                    return true;
            }
            else
            {
                for (int i = 0; i < words.Length; i++)
                {
                    if (words[i] == term)
                        return true;
                }
            }
        }

        return false;
    }

    private static bool HasTooManyRepeatedChars(string text, int maxSame)
    {
        if (maxSame < 1 || text.Length == 0)
            return false;
        int run = 1;
        for (int i = 1; i < text.Length; i++)
        {
            if (text[i] == text[i - 1] && !char.IsWhiteSpace(text[i]))
            {
                run++;
                if (run > maxSame)
                    return true;
            }
            else
            {
                run = 1;
            }
        }

        return false;
    }

    private static bool HasRepeatedWord(string[] words, int maxCopies)
    {
        if (words == null || words.Length == 0 || maxCopies < 1)
            return false;
        int run = 1;
        string prev = words[0];
        for (int i = 1; i < words.Length; i++)
        {
            if (words[i] == prev)
            {
                run++;
                if (run > maxCopies)
                    return true;
            }
            else
            {
                run = 1;
                prev = words[i];
            }
        }

        return false;
    }

    private static bool HasRepetitivePhrase(string[] words, int maxPatternLen, int minRepeats)
    {
        if (words == null || words.Length == 0)
            return false;
        int n = words.Length;
        maxPatternLen = Math.Max(1, Math.Min(maxPatternLen, n / 2));
        minRepeats = Math.Max(2, minRepeats);
        for (int pl = 1; pl <= maxPatternLen; pl++)
        {
            if (n < pl * minRepeats)
                continue;
            for (int start = 0; start <= n - pl * minRepeats; start++)
            {
                bool allEqual = true;
                for (int r = 1; r < minRepeats && allEqual; r++)
                {
                    for (int i = 0; i < pl; i++)
                    {
                        if (words[start + (r - 1) * pl + i] != words[start + r * pl + i])
                        {
                            allEqual = false;
                            break;
                        }
                    }
                }

                if (allEqual)
                    return true;
            }
        }

        return false;
    }

    private VoiceInfo FindVoice(Settings settings, string input)
    {
        string want = (input ?? "").Trim();
        if (want.Length == 0)
            return null;
        foreach (var v in settings.Catalog)
        {
            if (Eq(v.Alias, want) || Eq(v.Display, want))
                return v;
        }

        return null;
    }

    private VoiceInfo FindVoiceByAlias(Settings settings, string input)
    {
        string want = (input ?? "").Trim();
        if (want.Length == 0)
            return null;
        foreach (var v in settings.Catalog)
        {
            if (Eq(v.Alias, want))
                return v;
        }

        return null;
    }

    private void Chat(string template, string user, EventContext ev, Settings settings, Dictionary<string, string> extra)
    {
        if (string.IsNullOrWhiteSpace(template))
            return;
        var vars = BaseVars(user, settings);
        if (extra != null)
        {
            foreach (var kv in extra)
                vars[kv.Key] = kv.Value ?? "";
        }

        Reply(Fc.ApplyTemplate(template, vars), ev);
    }

    private void Reply(string message, EventContext ev)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        if (!string.IsNullOrWhiteSpace(ev.MessageId))
            CPH.TwitchReplyToMessage(message, ev.MessageId);
        else
            CPH.SendMessage(message);
    }

    private Dictionary<string, string> BaseVars(string user, Settings settings)
    {
        string voices = string.Join(", ", settings.Catalog.Select(v => v.Display + " (" + v.Alias + ")"));
        var def = FindVoice(settings, settings.DefaultVoice);
        var sample = settings.Catalog.FirstOrDefault(v => !Eq(v.Alias, settings.DefaultVoice)) ?? def;
        string prefixEx = (sample != null ? sample.Alias : "fr") + " ";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["user"] = user ?? "",
            ["alias"] = def?.Alias ?? "",
            ["style"] = "",
            ["seconds"] = settings.CooldownSeconds.ToString(CultureInfo.InvariantCulture),
            ["styles"] = def == null ? "none" : FormatStyles(def),
            ["prefix"] = prefixEx,
            ["voices"] = voices,
            ["owned"] = "",
            ["sticky"] = "",
            ["max"] = settings.MaxChars.ToString(CultureInfo.InvariantCulture),
        };
    }

    private Dictionary<string, string> VarsForVoice(VoiceInfo voice, Settings settings)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (voice == null)
            return d;
        d["alias"] = voice.Alias;
        d["styles"] = FormatStyles(voice);
        d["style"] = voice.Styles.Length > 0 ? voice.Styles[0] : "";
        d["prefix"] = voice.Alias + " ";
        d["voices"] = string.Join(", ", settings.Catalog.Select(v => v.Display + " (" + v.Alias + ")"));
        return d;
    }

    private Dictionary<string, string> VarsFromParse(PrefixParse parsed, Settings settings)
    {
        var d = parsed.Voice != null ? VarsForVoice(parsed.Voice, settings) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(parsed.Style))
            d["style"] = parsed.Style;
        if (!string.IsNullOrEmpty(parsed.RawPrefix))
            d["prefix"] = parsed.RawPrefix;
        d["max"] = settings.MaxChars.ToString(CultureInfo.InvariantCulture);
        return d;
    }

    private Dictionary<string, string> HelpVars(Settings settings)
    {
        return VarsForVoice(settings.Catalog.FirstOrDefault(v => !Eq(v.Alias, settings.DefaultVoice)) ?? settings.Catalog[0], settings);
    }

    private static string FormatStyles(VoiceInfo voice)
    {
        if (voice == null || voice.Styles == null || voice.Styles.Length == 0)
            return "none";
        return string.Join(", ", voice.Styles);
    }

    private string TemplateFor(Settings s, string key)
    {
        switch (key)
        {
            case "paused":
                return s.MsgPaused;
            case "cooldown":
                return s.MsgCooldown;
            case "unpaid":
                return s.MsgUnpaid;
            case "unknown_prefix":
                return s.MsgUnknownPrefix;
            case "unknown_style":
                return s.MsgUnknownStyle;
            case "empty":
                return s.MsgEmpty;
            case "too_long":
                return s.MsgTooLong;
            case "url":
                return s.MsgUrl;
            case "spam":
            case "spam_char":
            case "spam_word":
            case "spam_phrase":
            case "spam_unique":
            case "spam_blocked":
                return s.MsgSpam;
            default:
                return s.MsgUnpaid;
        }
    }

    private static string MentionName(EventContext ev)
    {
        return MessageTemplates.SanitizeMention(!string.IsNullOrWhiteSpace(ev.User) ? ev.User : ev.UserName);
    }

    private bool IsModOrBroadcaster(EventContext ev)
    {
        if (ev.IsModerator)
            return true;
        if (CPH.TryGetArg("isBroadcaster", out bool b) && b)
            return true;
        try
        {
            var broadcaster = CPH.TwitchGetBroadcaster();
            if (broadcaster != null && (!string.IsNullOrWhiteSpace(ev.UserId) && string.Equals(broadcaster.UserId, ev.UserId, StringComparison.OrdinalIgnoreCase) || Eq(broadcaster.UserName, ev.User) || Eq(broadcaster.UserName, ev.UserName)))
                return true;
        }
        catch
        {
        // ignore
        }

        return Eq(ev.UserType, "broadcaster");
    }

    private static bool Eq(string a, string b)
    {
        return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static double ClampDouble(double value, double min, double max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static long UnixMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
#endregion
}