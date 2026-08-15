using FluentConfig;
using FluentConfig.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Streamer.bot.Plugin.Interface.Model;

// Refs: System, System.Core, FluentConfig.dll (Streamer.bot dlls/).
#if EXTERNAL_EDITOR
public class FiresaleMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "Fire Sale";
    private const string Version = "1.0.0";
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private class Settings
    {
        public string DefaultMode { get; set; } = "discount";
        public double DefaultMultiplier { get; set; } = 0.5;
        public double RandomMin { get; set; } = 0.1;
        public double RandomMax { get; set; } = 0.9;
        public string DefaultDuration { get; set; } = "permanent";
        public string TimerId { get; set; } = "";
        public bool ReplaceIfActive { get; set; }
        public string[] IncludeGroups { get; set; } = new string[0];
        public string[] ExcludeGroups { get; set; } = new string[0];
        public bool SendMessage { get; set; } = true;
        public string MsgProcessing { get; set; } = "🔥 Fire sale activating! Updating {count} rewards ({mode})...";
        public string MsgStarted { get; set; } = "✅ Fire sale is ACTIVE! {mode} on {groups} ({count} rewards){duration}";
        public string MsgAlreadyActive { get; set; } = "🔥 Fire sale is already active! Use !firesale off first.";
        public string MsgStatusOn { get; set; } = "🔥 Fire sale is ACTIVE! {mode} on {groups} ({count} rewards){remaining}";
        public string MsgStatusOff { get; set; } = "🔥 Fire sale is currently OFF.";
        public string MsgEnded { get; set; } = "✅ Fire sale ended! Restored {count} rewards to original prices.";
        public string MsgNoRewards { get; set; } = "No channel point rewards matched the configured groups.";
    }

    private class RewardCost
    {
        public string RewardId { get; set; }
        public int Cost { get; set; }
    }

    private class SaleState
    {
        public bool Active { get; set; }
        public string Mode { get; set; } = "";
        public double Multiplier { get; set; }
        public double RandomMin { get; set; }
        public double RandomMax { get; set; }
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string GroupsLabel { get; set; } = "all rewards";
        public int RewardCount { get; set; }
        public List<RewardCost> OriginalCosts { get; set; }
    }

    private class Plan
    {
        public string Mode { get; set; } = "discount";
        public double Multiplier { get; set; } = 0.5;
        public double RandomMin { get; set; } = 0.1;
        public double RandomMax { get; set; } = 0.9;
        public int? DurationSeconds { get; set; }
    }

    public bool Execute()
    {
        var log = Fc.Logger(CPH, Title, Version);
        var settings = LoadSettings();
        CPH.TryGetArg("input0", out string input0);
        string cmd = (input0 ?? "").Trim().ToLowerInvariant();
        if (cmd == "status")
            return ShowStatus(settings, log);
        if (cmd == "off" || cmd == "reset" || cmd == "end" || cmd == "stop")
            return EndSale(settings, log, fromCommand: true);
        return StartSale(settings, log);
    }

    private Settings LoadSettings()
    {
        return Fc.LoadSettings<Settings>(CPH, Title, x =>
        {
            x.DefaultMode = NormalizeMode(x.DefaultMode);
            x.DefaultMultiplier = ClampMultiplier(x.DefaultMultiplier, 0.5);
            x.RandomMin = ClampMultiplier(x.RandomMin, 0.1);
            x.RandomMax = ClampMultiplier(x.RandomMax, 0.9);
            if (x.RandomMin > x.RandomMax)
            {
                double swap = x.RandomMin;
                x.RandomMin = x.RandomMax;
                x.RandomMax = swap;
            }

            if (string.IsNullOrWhiteSpace(x.DefaultDuration))
                x.DefaultDuration = "permanent";
            x.TimerId = (x.TimerId ?? "").Trim();
            x.IncludeGroups = NormalizeGroupArray(x.IncludeGroups);
            x.ExcludeGroups = NormalizeGroupArray(x.ExcludeGroups);
        });
    }

    private bool ShowStatus(Settings settings, ExtensionLogger log)
    {
        var state = Fc.LoadData<SaleState>(CPH, Title);
        if (state == null || !state.Active)
        {
            Chat(settings, settings.MsgStatusOff, TemplateFromState(state, settings, ended: true));
            return true;
        }

        Chat(settings, settings.MsgStatusOn, TemplateFromState(state, settings, ended: false));
        log.Info("Status requested while active.");
        return true;
    }

    private bool EndSale(Settings settings, ExtensionLogger log, bool fromCommand)
    {
        var state = Fc.LoadData<SaleState>(CPH, Title);
        if (state == null || !state.Active)
        {
            DisableTimer(settings.TimerId);
            if (fromCommand)
                Chat(settings, settings.MsgStatusOff, TemplateFromState(state, settings, ended: true));
            return true;
        }

        int restored = RestoreOriginalCosts(state, log);
        string groups = state.GroupsLabel;
        ClearState();
        DisableTimer(settings.TimerId);
        CPH.SetArgument("firesaleActive", false);
        CPH.SetArgument("firesaleRestored", restored);
        log.Info($"Ended sale; restored {restored} rewards.");
        if (fromCommand)
            Chat(settings, settings.MsgEnded, TemplateVars(restored, groups, "", 0, null, null));
        return true;
    }

    private bool StartSale(Settings settings, ExtensionLogger log)
    {
        var state = Fc.LoadData<SaleState>(CPH, Title);
        if (state != null && state.Active)
        {
            if (!settings.ReplaceIfActive)
            {
                Chat(settings, settings.MsgAlreadyActive, TemplateFromState(state, settings, ended: false));
                return false;
            }

            log.Info("Replacing active sale; restoring original prices first.");
            RestoreOriginalCosts(state, log);
            ClearState();
            DisableTimer(settings.TimerId);
        }

        var plan = ParsePlan(settings, log);
        var rewards = CPH.TwitchGetRewards();
        if (rewards == null || rewards.Count == 0)
        {
            Chat(settings, settings.MsgNoRewards, TemplateVars(0, GroupsLabel(settings.IncludeGroups), plan.Mode, plan.Multiplier, plan.DurationSeconds, null));
            return false;
        }

        var filtered = FilterRewards(rewards, settings.IncludeGroups, settings.ExcludeGroups);
        if (filtered.Count == 0)
        {
            Chat(settings, settings.MsgNoRewards, TemplateVars(0, GroupsLabel(settings.IncludeGroups), plan.Mode, plan.Multiplier, plan.DurationSeconds, null));
            return false;
        }

        Chat(settings, settings.MsgProcessing, TemplateVars(filtered.Count, GroupsLabel(settings.IncludeGroups), plan.Mode, plan.Multiplier, plan.DurationSeconds, null));
        var originals = filtered.Select(r => new RewardCost { RewardId = r.Id, Cost = r.Cost }).ToList();
        double applied = ApplyPrices(filtered, plan);
        var next = new SaleState
        {
            Active = true,
            Mode = plan.Mode,
            Multiplier = applied,
            RandomMin = plan.RandomMin,
            RandomMax = plan.RandomMax,
            StartTime = DateTime.Now.ToString(DateFormat, CultureInfo.InvariantCulture),
            GroupsLabel = GroupsLabel(settings.IncludeGroups),
            RewardCount = filtered.Count,
            OriginalCosts = originals,
        };
        ConfigureTimer(settings, plan, next, log);
        Fc.SaveData(CPH, Title, next);
        CPH.SetArgument("firesaleActive", true);
        CPH.SetArgument("firesaleMode", next.Mode);
        CPH.SetArgument("firesaleCount", next.RewardCount);
        CPH.SetArgument("firesaleMultiplier", next.Multiplier);
        CPH.SetArgument("firesaleDiscount", GetDiscountPercent(next.Multiplier));
        log.Info($"Started {next.Mode} on {next.RewardCount} rewards (multiplier {next.Multiplier:0.##}).");
        Chat(settings, settings.MsgStarted, TemplateFromState(next, settings, ended: false));
        return true;
    }

    private Plan ParsePlan(Settings settings, ExtensionLogger log)
    {
        CPH.TryGetArg("input0", out string input0);
        CPH.TryGetArg("input1", out string input1);
        CPH.TryGetArg("input2", out string input2);
        var plan = new Plan
        {
            Mode = settings.DefaultMode,
            Multiplier = settings.DefaultMultiplier,
            RandomMin = settings.RandomMin,
            RandomMax = settings.RandomMax,
            DurationSeconds = ParseDurationSeconds(settings.DefaultDuration),
        };
        string a = (input0 ?? "").Trim();
        string b = (input1 ?? "").Trim();
        string c = (input2 ?? "").Trim();
        if (string.Equals(a, "free", StringComparison.OrdinalIgnoreCase))
        {
            plan.Mode = "free";
            plan.DurationSeconds = DurationOrKeep(b, plan.DurationSeconds);
            return plan;
        }

        if (string.Equals(a, "random", StringComparison.OrdinalIgnoreCase))
        {
            plan.Mode = "random";
            if (TryParseRange(b, out double min, out double max))
            {
                plan.RandomMin = min;
                plan.RandomMax = max;
                plan.DurationSeconds = DurationOrKeep(c, plan.DurationSeconds);
            }
            else
            {
                plan.DurationSeconds = DurationOrKeep(b, plan.DurationSeconds);
            }

            return plan;
        }

        if (TryParseMultiplier(a, out double multiplier))
        {
            plan.Mode = "discount";
            plan.Multiplier = multiplier;
            plan.DurationSeconds = DurationOrKeep(b, plan.DurationSeconds);
            return plan;
        }

        if (LooksLikeDuration(a))
        {
            plan.DurationSeconds = DurationOrKeep(a, plan.DurationSeconds);
            return plan;
        }

        if (!string.IsNullOrEmpty(a))
            log.Warn($"Unknown argument '{a}'; using settings defaults.");
        return plan;
    }

    private int? DurationOrKeep(string raw, int? fallback)
    {
        if (!LooksLikeDuration(raw))
            return fallback;
        return ParseDurationSeconds(raw);
    }

    private double ApplyPrices(List<TwitchReward> rewards, Plan plan)
    {
        if (plan.Mode == "free")
        {
            foreach (var reward in rewards)
                CPH.UpdateRewardCost(reward.Id, 1);
            return 0;
        }

        double multiplier = plan.Multiplier;
        if (plan.Mode == "random")
        {
            double min = Math.Min(plan.RandomMin, plan.RandomMax);
            double max = Math.Max(plan.RandomMin, plan.RandomMax);
            multiplier = min + new Random().NextDouble() * (max - min);
        }

        foreach (var reward in rewards)
        {
            int next = Math.Max(1, (int)Math.Round(reward.Cost * multiplier));
            CPH.UpdateRewardCost(reward.Id, next);
        }

        return multiplier;
    }

    private void ConfigureTimer(Settings settings, Plan plan, SaleState state, ExtensionLogger log)
    {
        if (plan.DurationSeconds == null || plan.DurationSeconds.Value <= 0)
        {
            state.EndTime = "";
            DisableTimer(settings.TimerId);
            return;
        }

        int seconds = plan.DurationSeconds.Value;
        state.EndTime = DateTime.Now.AddSeconds(seconds).ToString(DateFormat, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(settings.TimerId))
        {
            log.Warn("Timed sale requested but timer Id is empty in settings; sale will not auto-end.");
            return;
        }

        CPH.EnableTimerById(settings.TimerId);
        CPH.SetTimerInterval(settings.TimerId, seconds);
    }

    private List<TwitchReward> FilterRewards(List<TwitchReward> rewards, string[] includeGroups, string[] excludeGroups)
    {
        var exclude = ParseGroupList(excludeGroups);
        var include = ParseGroupList(includeGroups);
        return rewards.Where(r => r != null && !string.IsNullOrWhiteSpace(r.Id)).Where(r => include.Count == 0 || include.Contains(NormalizeGroup(r.Group))).Where(r => exclude.Count == 0 || !exclude.Contains(NormalizeGroup(r.Group))).ToList();
    }

    private static HashSet<string> ParseGroupList(string[] names)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (names == null)
            return set;
        foreach (string raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            foreach (string part in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim().ToLowerInvariant();
                if (name.Length > 0 && name != "none" && name != "null")
                    set.Add(name);
            }
        }

        return set;
    }

    private static string[] NormalizeGroupArray(string[] names)
    {
        var list = new List<string>();
        foreach (string name in ParseGroupList(names))
            list.Add(name);
        return list.ToArray();
    }

    private static string NormalizeGroup(string group) => (group ?? "").Trim().ToLowerInvariant();
    private static string GroupsLabel(string[] includeGroups)
    {
        if (includeGroups == null || includeGroups.Length == 0)
            return "all rewards";
        return string.Join(", ", includeGroups);
    }

    private int RestoreOriginalCosts(SaleState state, ExtensionLogger log)
    {
        if (state.OriginalCosts == null || state.OriginalCosts.Count == 0)
            return 0;
        int restored = 0;
        foreach (var cost in state.OriginalCosts)
        {
            if (string.IsNullOrWhiteSpace(cost.RewardId))
                continue;
            try
            {
                CPH.UpdateRewardCost(cost.RewardId, cost.Cost);
                restored++;
            }
            catch (Exception ex)
            {
                log.Warn($"Could not restore reward {cost.RewardId}: {ex.Message}");
            }
        }

        return restored;
    }

    private void ClearState()
    {
        Fc.SaveData(CPH, Title, new SaleState());
    }

    private void DisableTimer(string timerId)
    {
        if (string.IsNullOrWhiteSpace(timerId))
            return;
        CPH.DisableTimerById(timerId.Trim());
    }

    private void Chat(Settings settings, string template, Dictionary<string, string> vars)
    {
        if (!settings.SendMessage || string.IsNullOrWhiteSpace(template))
            return;
        CPH.SendMessage(Fc.ApplyTemplate(template, vars));
    }

    private Dictionary<string, string> TemplateFromState(SaleState state, Settings settings, bool ended)
    {
        if (state == null || ended || !state.Active)
            return TemplateVars(0, "all rewards", "", 0, null, null);
        return TemplateVars(state.RewardCount, state.GroupsLabel, state.Mode, state.Multiplier, ParseStoredEndSeconds(state.EndTime), RemainingText(state.EndTime));
    }

    private static Dictionary<string, string> TemplateVars(int count, string groups, string mode, double multiplier, int? durationSeconds, string remaining)
    {
        int discount = GetDiscountPercent(multiplier);
        string durationText = "";
        if (durationSeconds != null && durationSeconds.Value > 0)
            durationText = $" for {FormatDuration(durationSeconds.Value)}";
        string remainingText = string.IsNullOrEmpty(remaining) ? "" : $". Time remaining: {remaining}";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["count"] = count.ToString(),
            ["groups"] = groups ?? "all rewards",
            ["mode"] = ModeLabel(mode, multiplier, discount),
            ["discount"] = discount.ToString(),
            ["multiplier"] = multiplier.ToString("0.##", CultureInfo.InvariantCulture),
            ["duration"] = durationText,
            ["remaining"] = remainingText,
        };
    }

    private static string ModeLabel(string mode, double multiplier, int discount)
    {
        if (mode == "free")
            return "FREE (1 point)";
        if (mode == "random")
            return $"RANDOM {discount}% off";
        if (discount > 0)
            return $"{discount}% off";
        if (multiplier > 0)
            return $"×{multiplier.ToString("0.##", CultureInfo.InvariantCulture)}";
        return "sale";
    }

    private static int GetDiscountPercent(double multiplier)
    {
        if (multiplier <= 0)
            return 100;
        return (int)Math.Round((1 - multiplier) * 100);
    }

    private static string RemainingText(string endTimeStr)
    {
        if (!TryParseEnd(endTimeStr, out DateTime end))
            return "";
        TimeSpan left = end - DateTime.Now;
        if (left.TotalSeconds <= 0)
            return "";
        return FormatDuration((int)Math.Ceiling(left.TotalSeconds));
    }

    private static int? ParseStoredEndSeconds(string endTimeStr)
    {
        if (!TryParseEnd(endTimeStr, out DateTime end))
            return null;
        int seconds = (int)Math.Ceiling((end - DateTime.Now).TotalSeconds);
        return seconds > 0 ? seconds : (int? )null;
    }

    private static bool TryParseEnd(string endTimeStr, out DateTime end)
    {
        return DateTime.TryParseExact(endTimeStr, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out end);
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60)
            return $"{seconds} seconds";
        int minutes = (int)Math.Ceiling(seconds / 60.0);
        if (minutes < 60)
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        int hours = (int)Math.Round(minutes / 60.0);
        return hours == 1 ? "1 hour" : $"{hours} hours";
    }

    private static bool TryParseMultiplier(string raw, out double multiplier)
    {
        multiplier = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        string text = raw.Trim();
        if (text.EndsWith("%", StringComparison.Ordinal))
        {
            if (!double.TryParse(text.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                return false;
            if (percent < 1 || percent > 99)
                return false;
            multiplier = ClampMultiplier(1 - percent / 100.0, 0.5);
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return false;
        if (value < 0.01 || value > 1)
            return false;
        multiplier = value;
        return true;
    }

    private static bool TryParseRange(string raw, out double min, out double max)
    {
        min = 0;
        max = 0;
        if (string.IsNullOrWhiteSpace(raw) || raw.IndexOf('-') < 0)
            return false;
        string[] parts = raw.Split(new[] { '-' }, 2);
        if (parts.Length != 2)
            return false;
        if (!TryParseMultiplier(parts[0], out min) || !TryParseMultiplier(parts[1], out max))
            return false;
        if (min > max)
        {
            double swap = min;
            min = max;
            max = swap;
        }

        return true;
    }

    private static bool LooksLikeDuration(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        string lower = raw.Trim().ToLowerInvariant();
        if (lower == "permanent" || lower == "off" || lower == "none")
            return true;
        return ParseDurationSeconds(raw) != null;
    }

    private static int? ParseDurationSeconds(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string value = raw.Trim().ToLowerInvariant();
        if (value == "permanent" || value == "off" || value == "none")
            return null;
        var fullUnits = new[]
        {
            "seconds",
            "minutes",
            "hours",
            "mins",
            "min",
            "hour",
            "days",
            "weeks"
        };
        foreach (string unit in fullUnits)
        {
            if (!value.EndsWith(unit, StringComparison.Ordinal))
                continue;
            string numStr = value.Substring(0, value.Length - unit.Length).Trim();
            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                return null;
            return ToSeconds(num, unit);
        }

        char last = value[value.Length - 1];
        if (last == 's' || last == 'm' || last == 'h' || last == 'd' || last == 'w')
        {
            string numStr = value.Substring(0, value.Length - 1).Trim();
            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                return null;
            return ToSeconds(num, last.ToString());
        }

        return null;
    }

    private static int ToSeconds(double num, string unit)
    {
        switch (unit)
        {
            case "s":
            case "seconds":
                return Math.Max(1, (int)Math.Round(num));
            case "m":
            case "min":
            case "mins":
            case "minutes":
                return Math.Max(1, (int)Math.Round(num * 60));
            case "h":
            case "hour":
            case "hours":
                return Math.Max(1, (int)Math.Round(num * 3600));
            case "d":
            case "days":
                return Math.Max(1, (int)Math.Round(num * 86400));
            case "w":
            case "weeks":
                return Math.Max(1, (int)Math.Round(num * 604800));
            default:
                return Math.Max(1, (int)Math.Round(num * 60));
        }
    }

    private static string NormalizeMode(string mode)
    {
        string value = (mode ?? "").Trim().ToLowerInvariant();
        if (value == "1" || value == "random")
            return "random";
        if (value == "2" || value == "free")
            return "free";
        return "discount";
    }

    private static double ClampMultiplier(double value, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        if (value < 0.01)
            return 0.01;
        if (value > 1)
            return 1;
        return value;
    }
}