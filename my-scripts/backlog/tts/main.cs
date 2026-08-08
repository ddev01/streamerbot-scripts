using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

#if EXTERNAL_EDITOR
public class TtsMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string SettingsKey = "FluentConfig_Settings_TTS Sell System";
    private const string PermanentExpiry = "permanent";
    private const string PurchaseLogPath = "logs/tts-purchases.log";

    // --- Execute (empty - each action calls a different function) ---
    public bool Execute()
    {
        return true;
    }

    // --- Public entry points (called by StreamerBot actions) ---
    public bool TtsSpeak() => DoTtsSpeak();
    public bool TtsVoices() => DoTtsVoices();
    public bool TtsInventory() => DoTtsInventory();
    public bool TtsUse() => DoTtsUse();
    public bool TtsBuy() => DoTtsBuy();
    public bool TtsConfirm() => DoTtsConfirm();
    public bool TtsCancel() => DoTtsCancel();

    #region Settings Loader

    private class TtsSettings
    {
        public string PointsVariableName { get; set; } = "points";
        public bool TtsCostsPoints { get; set; } = true;
        public string PricingMode { get; set; } = "flat";
        public double DefaultVoiceFlatPrice { get; set; } = 100;
        public double DefaultVoicePerCharPrice { get; set; } = 1;
        public string[] VoiceAliases { get; set; } = Array.Empty<string>();
        public string DefaultVoiceAlias { get; set; } = "";
        public Dictionary<string, List<(int price, string duration)>> VoicePrices { get; set; } = new Dictionary<string, List<(int, string)>>();
        // Anti-spam
        public bool AntiSpamEnabled { get; set; } = true;
        public bool AntiSpamRepetitivePhrase { get; set; } = true;
        public int AntiSpamRepetitivePhraseMinRepeats { get; set; } = 3;
        public int AntiSpamRepetitivePhrasePatternLen { get; set; } = 2;
        public bool AntiSpamCharacterRepeat { get; set; } = true;
        public int AntiSpamCharRepeatMax { get; set; } = 5;
        public bool AntiSpamUniquenessRatio { get; set; } = true;
        public double AntiSpamMinUniquenessRatio { get; set; } = 0.4;
        public bool AntiSpamMaxLength { get; set; } = true;
        public int AntiSpamMaxLengthChars { get; set; } = 500;
    }

    private TtsSettings LoadTtsSettings()
    {
        var s = new TtsSettings();
        try
        {
            var json = CPH.GetGlobalVar<string>(SettingsKey, true);
            if (string.IsNullOrEmpty(json)) return s;

            var obj = JObject.Parse(json);
            s.PointsVariableName = obj["points_variable_name"]?.ToString() ?? "points";
            s.TtsCostsPoints = obj["tts_costs_points"]?.Value<bool>() ?? true;
            s.PricingMode = obj["pricing_mode_value"]?.ToString() ?? obj["pricing_mode"]?.ToString() ?? "flat";
            s.DefaultVoiceFlatPrice = obj["default_voice_flat_price"]?.Value<double>() ?? 100;
            s.DefaultVoicePerCharPrice = obj["default_voice_per_char_price"]?.Value<double>() ?? 1;

            var aliasesArr = obj["tts_voice_aliases"] as JArray;
            s.VoiceAliases = aliasesArr?.Select(t => t?.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToArray() ?? Array.Empty<string>();

            var defaultVoiceToken = obj["default_voice_alias"];
            if (defaultVoiceToken != null && defaultVoiceToken.Type == JTokenType.Integer)
            {
                var idx = defaultVoiceToken.Value<int>();
                s.DefaultVoiceAlias = idx >= 0 && idx < s.VoiceAliases.Length ? s.VoiceAliases[idx] : (s.VoiceAliases.Length > 0 ? s.VoiceAliases[0] : "");
            }
            else
            {
                s.DefaultVoiceAlias = defaultVoiceToken?.ToString() ?? "";
            }
            if (string.IsNullOrEmpty(s.DefaultVoiceAlias) && s.VoiceAliases.Length > 0)
                s.DefaultVoiceAlias = s.VoiceAliases[0];

            foreach (var alias in s.VoiceAliases)
            {
                var key = alias + "_prices";
                var pricesArr = obj[key] as JArray;
                var tiers = new List<(int, string)>();
                if (pricesArr != null)
                {
                    foreach (var row in pricesArr.OfType<JObject>())
                    {
                        var price = row["price"]?.Value<int>() ?? 0;
                        var duration = row["duration"]?.ToString() ?? "permanent";
                        tiers.Add((price, duration));
                    }
                }
                s.VoicePrices[alias] = tiers;
            }

            s.AntiSpamEnabled = obj["antispam_enabled"]?.Value<bool>() ?? true;
            s.AntiSpamRepetitivePhrase = obj["antispam_repetitive_phrase"]?.Value<bool>() ?? true;
            s.AntiSpamRepetitivePhraseMinRepeats = obj["antispam_repetitive_min_repeats"]?.Value<int>() ?? 3;
            s.AntiSpamRepetitivePhrasePatternLen = obj["antispam_repetitive_pattern_len"]?.Value<int>() ?? 2;
            s.AntiSpamCharacterRepeat = obj["antispam_char_repeat"]?.Value<bool>() ?? true;
            s.AntiSpamCharRepeatMax = obj["antispam_char_repeat_max"]?.Value<int>() ?? 5;
            s.AntiSpamUniquenessRatio = obj["antispam_uniqueness_ratio"]?.Value<bool>() ?? true;
            s.AntiSpamMinUniquenessRatio = obj["antispam_min_uniqueness"]?.Value<double>() ?? 0.4;
            s.AntiSpamMaxLength = obj["antispam_max_length"]?.Value<bool>() ?? true;
            s.AntiSpamMaxLengthChars = obj["antispam_max_length_chars"]?.Value<int>() ?? 500;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[TTS] LoadTtsSettings error: {ex.Message}");
        }
        return s;
    }

    #endregion

    #region Duration Parsing

    /// <summary>Parses duration string to expiry DateTime. Returns null for permanent.</summary>
    private static DateTime? ParseDurationToExpiry(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        value = value.Trim().ToLowerInvariant();
        if (value == "permanent") return null;

        var fullUnits = new[] { "seconds", "minutes", "hours", "days", "weeks" };
        for (int u = 0; u < fullUnits.Length; u++)
        {
            if (value.EndsWith(fullUnits[u]))
            {
                var numStr = value.Substring(0, value.Length - fullUnits[u].Length).Trim();
                if (!int.TryParse(numStr, out var num)) return null;
                var span = u switch
                {
                    0 => TimeSpan.FromSeconds(num),
                    1 => TimeSpan.FromMinutes(num),
                    2 => TimeSpan.FromHours(num),
                    3 => TimeSpan.FromDays(num),
                    4 => TimeSpan.FromDays(num * 7),
                    _ => TimeSpan.Zero
                };
                return DateTime.UtcNow.Add(span);
            }
        }

        var unitChars = "smhdw";
        for (int i = value.Length - 1; i >= 0; i--)
        {
            var c = value[i];
            if (char.IsDigit(c) || c == ' ') continue;
            if (unitChars.IndexOf(c) >= 0)
            {
                var numStr = value.Substring(0, i).Trim();
                if (!int.TryParse(numStr, out var num)) return null;
                TimeSpan span = c switch
                {
                    's' => TimeSpan.FromSeconds(num),
                    'm' => TimeSpan.FromMinutes(num),
                    'h' => TimeSpan.FromHours(num),
                    'd' => TimeSpan.FromDays(num),
                    'w' => TimeSpan.FromDays(num * 7),
                    _ => TimeSpan.Zero
                };
                return DateTime.UtcNow.Add(span);
            }
            break;
        }
        return null;
    }

    /// <summary>Normalizes user input for duration matching (e.g. "7 days" -> "7days", "perm" -> "permanent").</summary>
    private static string NormalizeDurationInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var t = input.Trim().ToLowerInvariant();
        if (t == "perm" || t == "forever" || t == "unlimited") return "permanent";
        return new string(t.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    #endregion

    #region Twitch User Variables

    private bool GetTtsWarned(string userId)
    {
        return CPH.GetTwitchUserVarById<bool>(userId, "tts_warned", true);
    }

    private void SetTtsWarned(string userId, bool value)
    {
        CPH.SetTwitchUserVarById(userId, "tts_warned", value, true);
    }

    private class OwnedVoice
    {
        public string Voice { get; set; }
        public string ExpiresAt { get; set; } // ISO8601 date string, or PermanentExpiry for never-expiring
    }

    private static bool IsPermanent(string expiresAt) =>
        string.IsNullOrEmpty(expiresAt) || string.Equals(expiresAt, PermanentExpiry, StringComparison.OrdinalIgnoreCase);

    private List<OwnedVoice> GetTtsOwnedVoices(string userId)
    {
        var json = CPH.GetTwitchUserVarById<string>(userId, "tts_owned_voices", true);
        if (string.IsNullOrEmpty(json)) return new List<OwnedVoice>();
        try
        {
            var arr = JArray.Parse(json);
            return arr.Select(t =>
            {
                var o = t as JObject;
                return new OwnedVoice
                {
                    Voice = o?["voice"]?.ToString() ?? "",
                    ExpiresAt = o?["expiresAt"]?.ToString()
                };
            }).Where(v => !string.IsNullOrEmpty(v.Voice)).ToList();
        }
        catch { return new List<OwnedVoice>(); }
    }

    private void SetTtsOwnedVoices(string userId, List<OwnedVoice> list)
    {
        var arr = new JArray();
        foreach (var v in list)
        {
            var jo = new JObject { ["voice"] = v.Voice };
            jo["expiresAt"] = IsPermanent(v.ExpiresAt) ? PermanentExpiry : v.ExpiresAt;
            arr.Add(jo);
        }
        CPH.SetTwitchUserVarById(userId, "tts_owned_voices", arr.ToString(), true);
    }

    private string GetTtsActiveVoice(string userId)
    {
        return CPH.GetTwitchUserVarById<string>(userId, "tts_active_voice", true) ?? "";
    }

    private void SetTtsActiveVoice(string userId, string voice)
    {
        CPH.SetTwitchUserVarById(userId, "tts_active_voice", voice ?? "", true);
    }

    private class PendingPurchase
    {
        public string Voice { get; set; }
        public string Duration { get; set; }
        public int Price { get; set; }
        public string ExpiresAt { get; set; }
    }

    private PendingPurchase GetTtsPendingPurchase(string userId)
    {
        var json = CPH.GetTwitchUserVarById<string>(userId, "tts_pending_purchase", true);
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var o = JObject.Parse(json);
            return new PendingPurchase
            {
                Voice = o["voice"]?.ToString() ?? "",
                Duration = o["duration"]?.ToString() ?? "",
                Price = o["price"]?.Value<int>() ?? 0,
                ExpiresAt = o["expiresAt"]?.ToString()
            };
        }
        catch { return null; }
    }

    private void SetTtsPendingPurchase(string userId, PendingPurchase p)
    {
        if (p == null)
        {
            CPH.UnsetTwitchUserVarById(userId, "tts_pending_purchase", true);
            return;
        }
        var jo = new JObject { ["voice"] = p.Voice, ["duration"] = p.Duration, ["price"] = p.Price };
        jo["expiresAt"] = IsPermanent(p.ExpiresAt) ? PermanentExpiry : p.ExpiresAt;
        CPH.SetTwitchUserVarById(userId, "tts_pending_purchase", jo.ToString(), true);
    }

    #endregion

    #region Anti-Spam

    private static readonly char[] WordSeparators = { ' ' };
    private static readonly char[] TrimChars = { '.', ',', '!', '?', ':', ';', '"', '\'', '(', ')', '[', ']', '{', '}' };

    /// <summary>Returns true if the message appears to be spam. Checks repetitive phrases, character repeat, uniqueness ratio, and max length.</summary>
    private (bool isSpam, string reason) CheckAntiSpam(string message, TtsSettings settings)
    {
        if (!settings.AntiSpamEnabled)
            return (false, null);

        if (string.IsNullOrWhiteSpace(message))
            return (false, null);

        var normalizedMessage = NormalizeMessage(message);
        if (normalizedMessage.Length == 0)
            return (false, null);

        var words = normalizedMessage.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

        // 1. Max length
        if (settings.AntiSpamMaxLength && normalizedMessage.Length > settings.AntiSpamMaxLengthChars)
            return (true, "Message too long.");

        // 2. Repetitive phrase (sliding-window)
        if (settings.AntiSpamRepetitivePhrase && words.Length >= settings.AntiSpamRepetitivePhrasePatternLen)
        {
            if (HasRepetitivePhrase(words, settings.AntiSpamRepetitivePhrasePatternLen, settings.AntiSpamRepetitivePhraseMinRepeats))
                return (true, "Repetitive phrase detected.");
        }

        // 3. Character repetition (single pass)
        if (settings.AntiSpamCharacterRepeat && settings.AntiSpamCharRepeatMax > 0)
        {
            if (HasTooManyRepeatedChars(normalizedMessage, settings.AntiSpamCharRepeatMax))
                return (true, "Too many repeated characters.");
        }

        // 4. Uniqueness ratio (words normalized: strip punctuation)
        if (settings.AntiSpamUniquenessRatio && words.Length > 1)
        {
            var uniqueCount = words.Select(NormalizeWord).Where(w => w.Length > 0).Distinct().Count();
            var ratio = (double)uniqueCount / words.Length;
            if (ratio < settings.AntiSpamMinUniquenessRatio)
                return (true, "Too many repeated words.");
        }

        return (false, null);
    }

    private static string NormalizeMessage(string message)
    {
        var trimmed = message.Trim();
        var sb = new StringBuilder(trimmed.Length);
        bool prevSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            else
            {
                sb.Append(char.ToLowerInvariant(ch));
                prevSpace = false;
            }
        }
        return sb.ToString();
    }

    private static string NormalizeWord(string word)
    {
        return word.Trim(TrimChars).ToLowerInvariant();
    }

    private static bool HasTooManyRepeatedChars(string text, int maxSame)
    {
        if (text.Length == 0) return false;
        int currentRun = 1;
        for (int i = 1; i < text.Length; i++)
        {
            if (text[i] == text[i - 1])
            {
                currentRun++;
                if (currentRun > maxSame) return true;
            }
            else
            {
                currentRun = 1;
            }
        }
        return false;
    }

    private static bool HasRepetitivePhrase(string[] words, int maxPatternLen, int minRepeats)
    {
        maxPatternLen = Math.Max(1, Math.Min(maxPatternLen, words.Length / 2));
        minRepeats = Math.Max(2, minRepeats);

        for (int pl = 1; pl <= maxPatternLen; pl++)
        {
            if (words.Length < pl * minRepeats) continue;

            for (int start = 0; start <= words.Length - pl * minRepeats; start++)
            {
                bool allEqual = true;
                for (int r = 1; r < minRepeats && allEqual; r++)
                {
                    for (int i = 0; i < pl; i++)
                    {
                        var w1 = words[start + (r - 1) * pl + i];
                        var w2 = words[start + r * pl + i];
                        if (!w1.Equals(w2, StringComparison.OrdinalIgnoreCase))
                        {
                            allEqual = false;
                            break;
                        }
                    }
                }
                if (allEqual) return true;
            }
        }
        return false;
    }

    #endregion

    #region Args Helper

    private bool TryGetArgs(out string user, out string userId, out string rawInput)
    {
        user = null;
        userId = null;
        rawInput = null;
        return CPH.TryGetArg("user", out user)
            && CPH.TryGetArg("userId", out userId)
            && CPH.TryGetArg("rawInput", out rawInput);
    }

    #endregion

    #region Purchase Logging

    /// <summary>Logs a purchase to the purchase log file.</summary>
    private void LogPurchase(string user, string userId, string purchaseType, int cost, string voice = null, string duration = null, string message = null)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logParts = new List<string>
            {
                $"[{timestamp}]",
                $"User: {user}",
                $"UserId: {userId}",
                $"Type: {purchaseType}",
                $"Cost: {cost:N0} points"
            };

            if (!string.IsNullOrEmpty(voice))
                logParts.Add($"Voice: {voice}");
            
            if (!string.IsNullOrEmpty(duration))
                logParts.Add($"Duration: {duration}");
            
            if (!string.IsNullOrEmpty(message))
                logParts.Add($"Message: {message}");

            string logEntry = string.Join(" | ", logParts);

            // Ensure the logs directory exists
            string logDirectory = Path.GetDirectoryName(PurchaseLogPath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Append to log file
            File.AppendAllText(PurchaseLogPath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Fallback to CPH logging if file writing fails
            CPH.LogError($"[TTS] Failed to write purchase log: {ex.Message}");
        }
    }

    #endregion

    #region TtsSpeak

    private bool DoTtsSpeak()
    {
        if (!TryGetArgs(out var user, out var userId, out var rawInput))
        {
            CPH.SendMessage("TTS: Missing user args.");
            return false;
        }

        var message = (rawInput ?? "").Trim();
        var settings = LoadTtsSettings();

        if (string.IsNullOrEmpty(settings.DefaultVoiceAlias) && (settings.VoiceAliases?.Length ?? 0) == 0)
        {
            CPH.SendMessage($"@{user}, TTS is not configured. No voices available.");
            return false;
        }

        // 1. First-time warning
        if (settings.TtsCostsPoints && !GetTtsWarned(userId))
        {
            var warnMsg = BuildCostWarningMessage(settings);
            CPH.SendMessage($"@{user}, {warnMsg}");
            SetTtsWarned(userId, true);
        }

        // 2. Determine voice (check active, handle expiry)
        var voice = ResolveActiveVoice(userId, user, settings);
        if (voice == null)
        {
            CPH.SendMessage($"@{user}, Could not resolve voice. Using default.");
            voice = settings.DefaultVoiceAlias;
        }

        if (string.IsNullOrEmpty(message))
        {
            CPH.SendMessage($"@{user}, Usage: !tts <message>");
            return false;
        }

        // 2.5. Anti-spam check
        var (isSpam, spamReason) = CheckAntiSpam(message, settings);
        if (isSpam)
        {
            CPH.SendMessage($"@{user}, Message blocked by anti-spam: {spamReason}");
            return false;
        }

        // 3. Charge points
        if (settings.TtsCostsPoints)
        {
            var cost = ComputeCost(message, settings);
            var pointsVar = settings.PointsVariableName;
            var currentStr = CPH.GetTwitchUserVarById<string>(userId, pointsVar, true);
            long currentPoints = 0;
            long.TryParse(currentStr, out currentPoints);
            if (currentPoints < cost)
            {
                CPH.SendMessage($"@{user}, Not enough points. Need {cost:N0}, you have {currentPoints:N0}.");
                return false;
            }
            CPH.SetTwitchUserVarById(userId, pointsVar, currentPoints - cost, true);
            
            // Log TTS purchase
            LogPurchase(user, userId, "TTS Usage", (int)cost, voice, null, message);
        }

        // 4. Speak
        CPH.TtsSpeak(voice, message, false);
        return true;
    }

    private string BuildCostWarningMessage(TtsSettings s)
    {
        var mode = s.PricingMode ?? "flat";
        if (mode == "flat")
            return $"Each TTS message costs {s.DefaultVoiceFlatPrice:N0} points.";
        if (mode == "per_char")
            return $"Each TTS message costs {s.DefaultVoicePerCharPrice:N0} points per character.";
        return $"Each TTS message costs {s.DefaultVoiceFlatPrice:N0} points plus {s.DefaultVoicePerCharPrice:N0} points per character.";
    }

    private double ComputeCost(string message, TtsSettings s)
    {
        var mode = s.PricingMode ?? "flat";
        var len = message?.Length ?? 0;
        if (mode == "flat") return s.DefaultVoiceFlatPrice;
        if (mode == "per_char") return s.DefaultVoicePerCharPrice * len;
        return s.DefaultVoiceFlatPrice + s.DefaultVoicePerCharPrice * len;
    }

    private string ResolveActiveVoice(string userId, string user, TtsSettings settings)
    {
        var active = GetTtsActiveVoice(userId);
        var owned = GetTtsOwnedVoices(userId);
        var defaultVoice = settings.DefaultVoiceAlias;

        // No active or active is default - use default
        if (string.IsNullOrEmpty(active) || string.Equals(active, defaultVoice, StringComparison.OrdinalIgnoreCase))
        {
            return defaultVoice;
        }

        // Check if active is in owned and still valid
        var activeEntry = owned.FirstOrDefault(o => string.Equals(o.Voice, active, StringComparison.OrdinalIgnoreCase));
        if (activeEntry != null)
        {
            if (IsPermanent(activeEntry.ExpiresAt))
                return active; // Permanent
            if (DateTime.TryParse(activeEntry.ExpiresAt, out var expires) && expires > DateTime.UtcNow)
                return active; // Still valid
        }

        // Active expired or not in owned - find fallback
        var validOwned = owned.Where(o =>
        {
            if (IsPermanent(o.ExpiresAt)) return true;
            return DateTime.TryParse(o.ExpiresAt, out var ex) && ex > DateTime.UtcNow;
        }).ToList();

        // Remove expired from owned
        var stillValid = owned.Where(o =>
        {
            if (IsPermanent(o.ExpiresAt)) return true;
            return DateTime.TryParse(o.ExpiresAt, out var ex) && ex > DateTime.UtcNow;
        }).ToList();
        if (stillValid.Count != owned.Count)
            SetTtsOwnedVoices(userId, stillValid);

        string fallback;
        if (validOwned.Count > 0)
        {
            fallback = validOwned[0].Voice;
            SetTtsActiveVoice(userId, fallback);
            CPH.SendMessage($"@{user}, Your active voice ({active}) has expired. Switching to {fallback}.");
        }
        else
        {
            fallback = defaultVoice;
            SetTtsActiveVoice(userId, fallback);
            CPH.SendMessage($"@{user}, Your active voice ({active}) has expired. You have no other voices. Switching to default ({defaultVoice}).");
        }
        return fallback;
    }

    #endregion

    #region TtsVoices

    private bool DoTtsVoices()
    {
        if (!TryGetArgs(out var user, out var userId, out _)) return false;

        var settings = LoadTtsSettings();
        if (settings.VoiceAliases == null || settings.VoiceAliases.Length == 0)
        {
            CPH.SendMessage($"@{user}, No voices are available. Configure TTS in settings.");
            return false;
        }

        var lines = new List<string>();
        foreach (var alias in settings.VoiceAliases)
        {
            if (!settings.VoicePrices.TryGetValue(alias, out var tiers) || tiers.Count == 0)
            {
                lines.Add($"{alias}: (no tiers)");
                continue;
            }
            foreach (var (price, duration) in tiers)
            {
                var durDisplay = FormatDurationForDisplay(duration);
                lines.Add($"{alias}: {price:N0} points for {durDisplay}");
            }
        }
        CPH.SendMessage($"@{user}, " + string.Join(" | ", lines));
        CPH.SendMessage($"@{user}, To buy: !ttsbuy <voice> <duration>");
        return true;
    }

    #endregion

    #region TtsInventory

    private bool DoTtsInventory()
    {
        if (!TryGetArgs(out var user, out var userId, out _))
            return false;

        var owned = GetTtsOwnedVoices(userId);
        var active = GetTtsActiveVoice(userId);
        var settings = LoadTtsSettings();
        var defaultVoice = settings.DefaultVoiceAlias ?? "";

        // Filter valid (non-expired) and optionally clean expired from storage
        var validOwned = new List<OwnedVoice>();
        var now = DateTime.UtcNow;
        foreach (var o in owned)
        {
            if (IsPermanent(o.ExpiresAt))
            {
                validOwned.Add(o);
                continue;
            }
            if (DateTime.TryParse(o.ExpiresAt, out var expires) && expires > now)
                validOwned.Add(o);
        }
        if (validOwned.Count != owned.Count)
            SetTtsOwnedVoices(userId, validOwned);

        if (validOwned.Count == 0)
        {
            var activeInfo = string.IsNullOrEmpty(active) || string.Equals(active, defaultVoice, StringComparison.OrdinalIgnoreCase)
                ? $"Using default ({defaultVoice})"
                : $"Active: {active}";
            CPH.SendMessage($"@{user}, You have no purchased voices. {activeInfo} | Use !ttsvoices to see what you can buy.");
            return true;
        }

        var lines = validOwned.Select(o =>
        {
            var dur = IsPermanent(o.ExpiresAt) ? PermanentExpiry : FormatTimeRemaining(o.ExpiresAt);
            var marker = string.Equals(o.Voice, active, StringComparison.OrdinalIgnoreCase) ? " (active)" : "";
            return $"{o.Voice}: {dur}{marker}";
        }).ToList();
        CPH.SendMessage($"@{user}, Your voices: " + string.Join(" | ", lines));
        CPH.SendMessage($"@{user}, Switch with: !ttsuse <voice>");
        return true;
    }

    /// <summary>Formats expiry ISO8601 string as human-readable remaining time.</summary>
    private static string FormatTimeRemaining(string expiresAt)
    {
        if (IsPermanent(expiresAt)) return PermanentExpiry;
        if (!DateTime.TryParse(expiresAt, out var exp)) return "?";
        var remaining = exp - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return "expired";
        if (remaining.TotalDays >= 1) return $"{(int)remaining.TotalDays}d left";
        if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours}h left";
        if (remaining.TotalMinutes >= 1) return $"{(int)remaining.TotalMinutes}m left";
        return $"{(int)remaining.TotalSeconds}s left";
    }

    #endregion

    #region TtsUse

    private bool DoTtsUse()
    {
        if (!TryGetArgs(out var user, out var userId, out var rawInput))
            return false;

        var voiceInput = (rawInput ?? "").Trim();
        if (string.IsNullOrEmpty(voiceInput))
        {
            CPH.SendMessage($"@{user}, Usage: !ttsuse <voice> | Use !ttsinventory to see your voices.");
            return false;
        }

        var settings = LoadTtsSettings();
        var defaultVoice = settings.DefaultVoiceAlias ?? "";

        // Allow "default" to switch back to stream default
        if (string.Equals(voiceInput, "default", StringComparison.OrdinalIgnoreCase))
        {
            SetTtsActiveVoice(userId, "");
            CPH.SendMessage($"@{user}, Active voice set to default ({defaultVoice}).");
            return true;
        }

        var owned = GetTtsOwnedVoices(userId);
        var validOwned = owned.Where(o =>
        {
            if (IsPermanent(o.ExpiresAt)) return true;
            return DateTime.TryParse(o.ExpiresAt, out var ex) && ex > DateTime.UtcNow;
        }).ToList();

        var match = validOwned.FirstOrDefault(o =>
            string.Equals(o.Voice, voiceInput, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            CPH.SendMessage($"@{user}, You don't own '{voiceInput}' or it has expired. Use !ttsinventory to see your voices.");
            return false;
        }

        SetTtsActiveVoice(userId, match.Voice);
        CPH.SendMessage($"@{user}, Active voice set to {match.Voice}.");
        return true;
    }

    #endregion

    private static string FormatDurationForDisplay(string d)
    {
        if (string.IsNullOrEmpty(d) || d == "permanent") return "permanent";
        d = d.Trim().ToLowerInvariant();
        var units = new[] { "seconds", "minutes", "hours", "days", "weeks" };
        foreach (var u in units)
        {
            if (d.EndsWith(u))
            {
                var num = d.Substring(0, d.Length - u.Length).Trim();
                return $"{num} {u}";
            }
        }
        return d;
    }

    #region TtsBuy

    private bool DoTtsBuy()
    {
        if (!TryGetArgs(out var user, out var userId, out var rawInput))
            return false;

        var input = (rawInput ?? "").Trim();
        if (string.IsNullOrEmpty(input))
        {
            CPH.SendMessage($"@{user}, Usage: !ttsbuy <voice> <duration>");
            return false;
        }

        var settings = LoadTtsSettings();
        if (settings.VoiceAliases == null || settings.VoiceAliases.Length == 0)
        {
            CPH.SendMessage($"@{user}, No voices available.");
            return false;
        }

        var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            CPH.SendMessage($"@{user}, Usage: !ttsbuy <voice> <duration> (e.g. !ttsbuy French permanent)");
            return false;
        }

        var timeUnits = new[] { "days", "day", "hours", "hour", "minutes", "minute", "seconds", "second", "weeks", "week" };
        var lastPart = parts[parts.Length - 1].ToLowerInvariant();
        string durationInputRaw;
        int durationPartCount;
        if (parts.Length >= 3 && timeUnits.Contains(lastPart))
        {
            durationInputRaw = parts[parts.Length - 2] + " " + parts[parts.Length - 1];
            durationPartCount = 2;
        }
        else
        {
            durationInputRaw = parts[parts.Length - 1];
            durationPartCount = 1;
        }
        var durationInput = NormalizeDurationInput(durationInputRaw);
        var voiceInput = string.Join(" ", parts.Take(parts.Length - durationPartCount)).Trim();

        var matchingAlias = settings.VoiceAliases.FirstOrDefault(a =>
            string.Equals(a, voiceInput, StringComparison.OrdinalIgnoreCase));
        if (matchingAlias == null)
        {
            CPH.SendMessage($"@{user}, Unknown voice '{voiceInput}'. Use !ttsvoices to see available voices.");
            return false;
        }

        if (!settings.VoicePrices.TryGetValue(matchingAlias, out var tiers) || tiers.Count == 0)
        {
            CPH.SendMessage($"@{user}, No pricing tiers for {matchingAlias}.");
            return false;
        }

        (int price, string duration)? matchedTier = null;
        foreach (var (price, duration) in tiers)
        {
            var normalized = NormalizeDurationInput(duration);
            if (normalized == durationInput || string.Equals(normalized, durationInput, StringComparison.OrdinalIgnoreCase))
            {
                matchedTier = (price, duration);
                break;
            }
        }

        if (matchedTier == null)
        {
            CPH.SendMessage($"@{user}, No tier matches duration '{durationInputRaw}' for {matchingAlias}. Use !ttsvoices to see options.");
            return false;
        }

        var expiresAt = ParseDurationToExpiry(matchedTier.Value.duration);
        var expiresStr = expiresAt?.ToString("o") ?? null;

        SetTtsPendingPurchase(userId, new PendingPurchase
        {
            Voice = matchingAlias,
            Duration = matchedTier.Value.duration,
            Price = matchedTier.Value.price,
            ExpiresAt = expiresStr
        });

        var durDisplay = FormatDurationForDisplay(matchedTier.Value.duration);
        CPH.SendMessage($"@{user}, You're about to purchase {matchingAlias} TTS for {matchedTier.Value.price:N0} points ({durDisplay}). Type !ttsconfirm to continue or !ttscancel to cancel.");
        return true;
    }

    #endregion

    #region TtsConfirm

    private bool DoTtsConfirm()
    {
        if (!TryGetArgs(out var user, out var userId, out _))
            return false;

        var pending = GetTtsPendingPurchase(userId);
        if (pending == null || string.IsNullOrEmpty(pending.Voice))
        {
            CPH.SendMessage($"@{user}, No purchase pending.");
            return false;
        }

        var pointsVar = LoadTtsSettings().PointsVariableName;
        var currentStr = CPH.GetTwitchUserVarById<string>(userId, pointsVar, true);
        long currentPoints = 0;
        long.TryParse(currentStr, out currentPoints);

        if (currentPoints < pending.Price)
        {
            CPH.SendMessage($"@{user}, Not enough points. Need {pending.Price:N0}, you have {currentPoints:N0}.");
            return false;
        }

        CPH.SetTwitchUserVarById(userId, pointsVar, currentPoints - pending.Price, true);

        var owned = GetTtsOwnedVoices(userId);
        owned.Add(new OwnedVoice { Voice = pending.Voice, ExpiresAt = IsPermanent(pending.ExpiresAt) ? PermanentExpiry : pending.ExpiresAt });
        SetTtsOwnedVoices(userId, owned);
        SetTtsActiveVoice(userId, pending.Voice);
        SetTtsPendingPurchase(userId, null);

        var durDisplay = FormatDurationForDisplay(pending.Duration);
        CPH.SendMessage($"@{user}, You purchased {pending.Voice} TTS ({durDisplay}). It is now your active voice!");
        
        // Log voice purchase
        LogPurchase(user, userId, "Voice Purchase", pending.Price, pending.Voice, pending.Duration);
        return true;
    }

    #endregion

    #region TtsCancel

    private bool DoTtsCancel()
    {
        if (!TryGetArgs(out var user, out var userId, out _))
            return false;

        SetTtsPendingPurchase(userId, null);
        CPH.SendMessage($"@{user}, Purchase cancelled.");
        return true;
    }

    #endregion
}
