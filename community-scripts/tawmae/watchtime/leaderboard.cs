using System;
using System.Collections.Generic;
using System.Linq;
using TawmaeUI;

#if EXTERNAL_EDITOR
public class WatchtimeLeaderboard : CPHInlineBase
#else
public class CPHInline
#endif
{
    public bool Execute()
    {
        if (extensionName == null || extensionVersion == null)
        {
            CPH.ExecuteMethod("TWITCH WATCHTIME Version Check", "Execute");
            CPH.TryGetArg("extensionName", out extensionName);
            CPH.TryGetArg("extensionVersion", out extensionVersion);
        }

        if (
            string.IsNullOrEmpty(CPH.GetGlobalVar<string>($"tawmae_Settings_{extensionName}", true))
        )
        {
            ReplyToChat(
                $"Please initialize the settings first by opening the Settings UI of '{extensionName}' and saving afterwards."
            );
            PostToLog("Skipping, because the Settings UI has not been initialized yet.");
            return false;
        }

        BroadcasterAndBotCheck();
        FetchSettings();
        CPH.TryGetArg("user", out string redeemerName);
        CPH.TryGetArg("userId", out string redeemerId);
        var allVars = CPH.GetTwitchUsersVar<long>(globalVariableName, true);
        if (allVars == null || allVars.Count == 0)
        {
            ReplyToChat(
                $"Can't create a leaderboard, because there are no users with watchtime data :("
            );
            PostToLog(
                $"Aborting because there are no users with the variable '{globalVariableName}' set."
            );
            return false;
        }

        var filtered = new List<UserVariableValue<long>>();
        var excludedLogins = new HashSet<string>(excludedBotUsernames);
        if (!string.IsNullOrEmpty(broadcasterUserName))
            excludedLogins.Add(broadcasterUserName.ToLower());
        if (!string.IsNullOrEmpty(botUserName))
            excludedLogins.Add(botUserName.ToLower());
        if (excludedGroup != "< Select Your Group >")
        {
            var groupUsers = CPH.UsersInGroup(excludedGroup);
            foreach (var gu in groupUsers)
                excludedLogins.Add(gu.Login.ToLower());
        }

        foreach (var uv in allVars)
        {
            if (excludedLogins.Contains(uv.UserLogin.ToLower()))
                continue;
            filtered.Add(uv);
        }

        if (filtered.Count == 0)
        {
            ReplyToChat(
                $"All users with watchtime data are excluded from the leaderboard, so there's nobody left to show :("
            );
            PostToLog(
                $"Aborting because all users with the variable '{globalVariableName}' are excluded."
            );
            return false;
        }

        var sorted = filtered.OrderByDescending(u => u.Value).ToList();
        int showCount = Math.Min(rankAmount, sorted.Count);
        var parts = new List<string>();
        for (int i = 0; i < showCount; i++)
        {
            var u = sorted[i];
            string formatted = FormatWatchtime(u.Value);
            string entry = leaderboardFormat
                .Replace("%rank%", (i + 1).ToString())
                .Replace("%user%", u.UserName)
                .Replace("%watchtime%", formatted);
            parts.Add(entry);
        }

        bool redeemerInTop = sorted.Take(showCount).Any(u => u.UserId == redeemerId);
        if (showRedeemerRank && !redeemerInTop && sorted.Any(u => u.UserId == redeemerId))
        {
            int redeemerRank = sorted.FindIndex(u => u.UserId == redeemerId) + 1;
            var redeemerVar = sorted.First(u => u.UserId == redeemerId);
            string formattedRedeemer = FormatWatchtime(redeemerVar.Value);
            string redeemerEntry = leaderboardFormat
                .Replace("%rank%", redeemerRank.ToString())
                .Replace("%user%", redeemerVar.UserName)
                .Replace("%watchtime%", formattedRedeemer);
            parts.Add("... " + redeemerEntry);
        }

        string title = leaderboardTitle.Replace("%rankAmount%", rankAmount.ToString());
        if (oneMessagePerRank)
        {
            ReplyToChat(title);
            CPH.Wait(150);
            foreach (var part in parts)
            {
                ReplyToChat(part);
                CPH.Wait(150);
            }
        }
        else
        {
            string message = title + " " + string.Join(separator, parts);
            ReplyToChat(message);
        }

        return true;
    }

    public void BroadcasterAndBotCheck()
    {
        if (string.IsNullOrEmpty(broadcasterUserName))
        {
            var bci = CPH.TwitchGetBroadcaster();
            if (bci != null)
            {
                broadcasterUserName = bci.UserLogin?.ToLower();
                excludedBotUsernames.Add(broadcasterUserName);
            }
        }

        if (string.IsNullOrEmpty(botUserName))
        {
            var boti = CPH.TwitchGetBot();
            if (boti != null)
            {
                botUserName = boti.UserLogin?.ToLower();
                excludedBotUsernames.Add(botUserName);
            }
            else
                botUserName = "n/a";
        }
    }

    private string FormatWatchtime(long totalSeconds)
    {
        var unitsOrder = timeFormat.Split(',').Select(u => u.Trim().ToLower()).ToList();
        var unitSeconds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "year", 31536000 },
            { "years", 31536000 },
            { "month", 2592000 },
            { "months", 2592000 },
            { "week", 604800 },
            { "weeks", 604800 },
            { "day", 86400 },
            { "days", 86400 },
            { "hour", 3600 },
            { "hours", 3600 },
            { "minute", 60 },
            { "minutes", 60 },
            { "second", 1 },
            { "seconds", 1 },
        };
        var translations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "year",
                (
                    ui.GetValue<string?>(extensionName, "twitch_watchtime_translationYears")
                    ?? "year/years"
                ).Split('/')
            },
            {
                "month",
                (
                    ui.GetValue<string?>(extensionName, "twitch_watchtime_translationMonths")
                    ?? "month/months"
                ).Split('/')
            },
            {
                "week",
                (
                    ui.GetValue<string?>(extensionName, "twitch_watchtime_translationWeeks")
                    ?? "week/weeks"
                ).Split('/')
            },
            {
                "day",
                (
                    ui.GetValue<string?>(extensionName, "twitch_watchtime_translationDays")
                    ?? "day/days"
                ).Split('/')
            },
            {
                "hour",
                (
                    ui.GetValue<string?>(extensionName, "twitch_watchtime_translationHours")
                    ?? "hour/hours"
                ).Split('/')
            },
            {
                "minute",
                (
                    ui.GetValue<string?>(extensionName, "twitch_watchtime_translationMinutes")
                    ?? "minute/minutes"
                ).Split('/')
            },
            {
                "second",
                (
                    ui.GetValue<string?>(extensionName, "twitch_watchtime_translationSeconds")
                    ?? "second/seconds"
                ).Split('/')
            },
        };
        var parts = new List<string>();
        long remainder = totalSeconds;
        foreach (var unit in unitsOrder)
        {
            if (!unitSeconds.TryGetValue(unit, out long secPerUnit))
                continue;
            long value = remainder / secPerUnit;
            remainder %= secPerUnit;
            if (value > 0)
            {
                var key = unit.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                    ? unit.Substring(0, unit.Length - 1)
                    : unit;
                if (!translations.ContainsKey(key))
                    key = "second";
                var trans = translations[key];
                string label = (value == 1 ? trans[0] : trans[1]);
                parts.Add($"{value} {label}");
            }
        }

        if (parts.Count == 0)
            return $"0 {translations["second"][1]}";
        if (parts.Count == 1)
            return parts[0];
        string andWord =
            ui.GetValue<string?>(extensionName, "twitch_watchtime_translationAnd") ?? "and";
        return string.Join(", ", parts.Take(parts.Count - 1)) + $" {andWord} " + parts.Last();
    }

    public void ReplyToChat(string text)
    {
        if (text == "-")
            return;
        CPH.TryGetArg("msgId", out string replyId);
        int maxLen = 480;
        for (int i = 0; i < text.Length; i += maxLen)
        {
            string part = text.Substring(i, Math.Min(maxLen, text.Length - i));
            if (postMessagesAsReplies && !string.IsNullOrEmpty(replyId))
                CPH.TwitchReplyToMessage(part, replyId);
            else
                CPH.SendMessage(part);
            CPH.Wait(150);
        }
    }

    public void PostToLog(string log)
    {
        CPH.LogInfo($"[{extensionName} v.{extensionVersion}] {log}");
    }

    public static string extensionName = null;
    public static string extensionVersion = null;
    private TawmaeUI.Tawmae ui;
    public static string broadcasterUserName;
    public static string botUserName;
    public bool postMessagesAsReplies;
    public int rankAmount;
    public string excludedGroup;
    public string separator;
    public bool showRedeemerRank;
    public string globalVariableName;
    public string timeFormat;
    public string leaderboardFormat;
    public bool oneMessagePerRank;
    public string leaderboardTitle;
    private static readonly HashSet<string> excludedBotUsernames = new HashSet<string>
    {
        "nightbot",
        "streamelements",
        "streamlabs",
        "sery_bot",
        "moobot",
        "fossabot",
        "wizebot",
        "scorpbot",
        "mixitupbot",
        "phantombot",
        "coebot",
        "stay_hydrated_bot",
        "pretzelrocks",
        "firebot",
        "botrixlive",
        "pokemoncommunitygame",
        "anotherttvviewer",
        "deepbot",
        "streampuppet",
        "vivbot",
        "lurxbots",
        "snazbot",
        "mtgbot",
        "moobotalpha",
        "streambot",
        "soundalerts",
        "kofistreambot",
        "tangiabot",
        "botrixoficial",
        "frostytoolsdotcom",
        "rocketrankbot",
        "wzbot",
        "commanderroot",
    };

    private void FetchSettings()
    {
        ui = new Tawmae(CPH, extensionName, extensionVersion, false);
        postMessagesAsReplies = ui.GetValue<bool>(
            extensionName,
            "twitch_watchtime_postMessagesAsReplies"
        );
        rankAmount = ui.GetValue<int>(extensionName, "twitch_watchtime_leaderboardRankAmount");
        excludedGroup =
            ui.GetValue<string?>(extensionName, "twitch_watchtime_leaderboardExcludedGroup")
            ?? "< Select Your Group >";
        separator =
            ui.GetValue<string?>(extensionName, "twitch_watchtime_leaderboardSeparator") ?? " // ";
        showRedeemerRank = ui.GetValue<bool>(
            extensionName,
            "twitch_watchtime_leaderboardShowRedeemerRank"
        );
        globalVariableName =
            ui.GetValue<string?>(extensionName, "twitch_watchtime_globalVariableName")
            ?? "watchtime";
        timeFormat =
            ui.GetValue<string?>(extensionName, "twitch_watchtime_timeFormat")
            ?? "Days, Hours, Minutes, Seconds";
        oneMessagePerRank =
            ui.GetValue<bool?>(extensionName, "twitch_watchtime_leaderboardOneMessagePerRank")
            ?? true;
        leaderboardFormat =
            ui.GetValue<string?>(extensionName, "twitch_watchtime_leaderboardFormat")
            ?? "%rank%. %user% (%watchtime%)";
        leaderboardTitle =
            ui.GetValue<string?>(extensionName, "twitch_watchtime_leaderboardTitle")
            ?? "Top %rankAmount% Watchtime Leaderboard:";
    }
}
