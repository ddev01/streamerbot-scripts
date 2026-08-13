using FluentConfig;
using FluentConfig.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

// Refs: System, System.Core, FluentConfig.dll (Streamer.bot dlls/).
// System + System.Core: GAC name/path - needed for LINQ and Newtonsoft JToken?.Value<T>().
#if EXTERNAL_EDITOR
public class FirstChattersMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "First Chatters";
    private const string Version = "1.0.0";
    private static readonly string CounterVar = Fc.KeyFor(Title, "counter");
    private static readonly string UsersVar = Fc.KeyFor(Title, "users");
    private static readonly string ActionIdVar = Fc.KeyFor(Title, "action_id");
    public bool Execute()
    {
        var log = Fc.Logger(CPH, Title, Version);
        RememberActionId();
        var settings = LoadSettings();
        int counter = CPH.GetGlobalVar<int>(CounterVar, true);
        if (counter >= settings.MaxCount)
        {
            CPH.LogDebug($"[First Chatters] All {settings.MaxCount} spots already claimed; disabling.");
            DisableSelf(log);
            return false;
        }

        var ev = Fc.CaptureEvent(CPH);
        string user = MessageTemplates.SanitizeMention(!string.IsNullOrWhiteSpace(ev.User) ? ev.User : ev.UserName);
        if (string.IsNullOrWhiteSpace(user))
        {
            log.Warn("No user argument; skip. Use a Twitch Chat Message trigger.");
            return false;
        }

        if (ShouldExcludeUser(user, settings))
            return false;
        var winners = LoadWinners(log);
        if (winners.Any(w => string.Equals(w, user, StringComparison.OrdinalIgnoreCase)))
        {
            CPH.LogDebug($"[First Chatters] {user} already claimed a spot; skip.");
            return false;
        }

        int positionIndex = counter; // 0-based
        int position = positionIndex + 1; // 1-based
        string ordinal = ToOrdinal(position);
        int points = settings.AwardPoints ? GetPointsForPlace(settings, position) : 0;
        winners.Add(user);
        CPH.SetGlobalVar(UsersVar, JArray.FromObject(winners).ToString(Newtonsoft.Json.Formatting.None), true);
        CPH.SetGlobalVar(CounterVar, counter + 1, true);
        if (settings.AwardPoints && points != 0)
            AwardPoints(user, ev.UserId, settings.PointsVariable, points, log);
        CPH.SetArgument("firstChattersUser", user);
        CPH.SetArgument("firstChattersPosition", position);
        CPH.SetArgument("firstChattersOrdinal", ordinal);
        CPH.SetArgument("firstChattersPoints", points);
        bool complete = counter + 1 >= settings.MaxCount;
        CPH.SetArgument("firstChattersComplete", complete);
        log.Info($"{user} claimed {ordinal} ({position}/{settings.MaxCount})" + (settings.AwardPoints ? $", {points} points" : "") + ".");
        if (settings.SendMessage && !string.IsNullOrWhiteSpace(settings.Message))
        {
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
                ["ordinal"] = ordinal,
                ["position"] = position.ToString(),
                ["points"] = points.ToString(),
            };
            CPH.SendMessage(Fc.ApplyTemplate(settings.Message, vars));
        }

        if (complete)
        {
            log.Info($"All {settings.MaxCount} spots claimed.");
            DisableSelf(log);
        }

        return true;
    }

    private void RememberActionId()
    {
        if (!CPH.TryGetArg("actionId", out string actionId) || string.IsNullOrWhiteSpace(actionId))
            return;
        CPH.SetGlobalVar(ActionIdVar, actionId, true);
    }

    private void DisableSelf(ExtensionLogger log)
    {
        RememberActionId();
        string actionId = CPH.GetGlobalVar<string>(ActionIdVar, true);
        if (string.IsNullOrWhiteSpace(actionId))
        {
            log.Warn("Cannot disable: no saved action id. Run this action once from a trigger first.");
            return;
        }

        CPH.DisableActionById(actionId);
        log.Info("Main action disabled until reset.");
    }

    private class Settings
    {
        public int MaxCount { get; set; } = 3;
        public bool ExcludeBroadcaster { get; set; } = true;
        public bool ExcludeBot { get; set; } = true;
        public bool SendMessage { get; set; } = true;
        public string Message { get; set; } = "Congrats @{user}, you're {ordinal}!";
        public bool AwardPoints { get; set; } = true;
        public string PointsVariable { get; set; } = "points";
        public int[] PlacePoints { get; set; } =
        {
            3000,
            1500,
            1000,
            750,
            600,
            500,
            428,
            375,
            333,
            300
        };
    }

    private Settings LoadSettings()
    {
        var s = Fc.LoadSettings<Settings>(CPH, Title, x =>
        {
            x.MaxCount = Clamp(x.MaxCount, 1, 10);
            if (string.IsNullOrWhiteSpace(x.PointsVariable))
                x.PointsVariable = "points";
            else
                x.PointsVariable = x.PointsVariable.Trim();
            if (x.PlacePoints == null || x.PlacePoints.Length < 10)
                x.PlacePoints = new int[10];
        });
        // Flat points_1..points_10 keys do not map onto PlacePoints[]; fill via GetSetting.
        for (int i = 0; i < 10; i++)
        {
            int value = Fc.GetSetting(CPH, Title, $"points_{i + 1}", 3000 / (i + 1));
            s.PlacePoints[i] = Math.Max(0, value);
        }

        return s;
    }

    private List<string> LoadWinners(ExtensionLogger log)
    {
        try
        {
            string raw = CPH.GetGlobalVar<string>(UsersVar, true);
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();
            var arr = JArray.Parse(raw);
            return arr.Select(t => t?.ToString() ?? "").Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        }
        catch (Exception ex)
        {
            log.Warn($"Could not parse winners list; starting empty. {ex.Message}");
            return new List<string>();
        }
    }

    private bool ShouldExcludeUser(string user, Settings settings)
    {
        if (settings.ExcludeBroadcaster)
        {
            var broadcaster = CPH.TwitchGetBroadcaster();
            if (broadcaster != null && string.Equals(user, broadcaster.UserName, StringComparison.OrdinalIgnoreCase))
            {
                CPH.LogDebug($"[First Chatters] Skip {user}: broadcaster excluded.");
                return true;
            }
        }

        if (settings.ExcludeBot)
        {
            if (KnownBots.IsKnownBot(user))
            {
                CPH.LogDebug($"[First Chatters] Skip {user}: known bot.");
                return true;
            }

            var bot = CPH.TwitchGetBot();
            if (bot != null && string.Equals(user, bot.UserName, StringComparison.OrdinalIgnoreCase))
            {
                CPH.LogDebug($"[First Chatters] Skip {user}: connected bot account.");
                return true;
            }
        }

        return false;
    }

    private static int GetPointsForPlace(Settings settings, int position)
    {
        int index = position - 1;
        if (index < 0 || index >= settings.PlacePoints.Length)
            return 0;
        return settings.PlacePoints[index];
    }

    private void AwardPoints(string userName, string eventUserId, string pointsVariable, int points, ExtensionLogger log)
    {
        string userId = null;
        if (!string.IsNullOrWhiteSpace(eventUserId))
            userId = eventUserId;
        else
        {
            var userInfo = CPH.TwitchGetUserInfoByLogin(userName);
            userId = userInfo?.UserId;
        }

        if (string.IsNullOrEmpty(userId))
        {
            log.Warn($"Could not resolve user id for {userName}; points not awarded.");
            return;
        }

        string currentStr = CPH.GetTwitchUserVarById<string>(userId, pointsVariable, true);
        long current = 0;
        if (!string.IsNullOrEmpty(currentStr))
            long.TryParse(currentStr, out current);
        long next = current + points;
        CPH.SetTwitchUserVarById(userId, pointsVariable, next, true);
        log.Info($"Awarded {points} {pointsVariable} to {userName} ({current} -> {next}).");
    }

    private static string ToOrdinal(int position)
    {
        int mod100 = position % 100;
        int mod10 = position % 10;
        string suffix = "th";
        if (mod100 < 11 || mod100 > 13)
        {
            if (mod10 == 1)
                suffix = "st";
            else if (mod10 == 2)
                suffix = "nd";
            else if (mod10 == 3)
                suffix = "rd";
        }

        return position + suffix;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }
}
