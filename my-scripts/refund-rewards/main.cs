using FluentConfig;
using FluentConfig.Runtime;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// Refs: System, System.Core, Newtonsoft.Json.dll, FluentConfig.dll (Streamer.bot dlls/).
#if EXTERNAL_EDITOR
public class RefundRewardsMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "Refund Rewards";
    private const string Version = "1.0.0";
    /// <summary>
    /// Paste the GUID from Streamer.bot: Timers → {Refund Rewards} Commit → Copy Timer Id.
    /// Export that timer with the actions so importers keep the same id.
    /// </summary>
    private const string CommitTimerId = "210f448e-0dc5-4913-b375-a557c6ded867";
    private static readonly string HistoryVar = Fc.KeyFor(Title, "history");
    private static readonly string PendingJobVar = Fc.KeyFor(Title, "pending");
    private const int HistoryCap = 500;
    private const string Usage = "!refund @user [count]";
    /// <summary>
    /// Source-holder no-op. Keep Execute C# Code disabled and call Track / OnUpdated / Refund / Cancel / Commit.
    /// If Code stays enabled, this must not run Refund() (command args are empty → chat usage → command loop).
    /// </summary>
    public bool Execute()
    {
        return true;
    }

    public bool Track()
    {
        var log = Fc.Logger(CPH, Title, Version);
        CPH.TryGetArg("rewardId", out string rewardId);
        CPH.TryGetArg("redemptionId", out string redemptionId);
        if (string.IsNullOrWhiteSpace(rewardId) || string.IsNullOrWhiteSpace(redemptionId))
        {
            log.Warn("Track skipped: missing rewardId or redemptionId.");
            return false;
        }

        CPH.TryGetArg("rewardName", out string rewardName);
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("userName", out string userName);
        int cost = 0;
        if (CPH.TryGetArg("rewardCost", out int costInt))
            cost = costInt;
        else if (CPH.TryGetArg("rewardCost", out long costLong))
            cost = costLong > int.MaxValue ? int.MaxValue : (int)costLong;
        var login = FirstNonEmpty(user, userName);
        var history = LoadHistory();
        history.RemoveAll(r => r.RedemptionId == redemptionId);
        history.Add(new RedemptionInfo { UserId = userId ?? "", UserLogin = login, RewardId = rewardId, RedemptionId = redemptionId, RewardName = rewardName ?? "", RewardCost = cost, RedeemedAt = DateTime.UtcNow.ToString("o"), });
        TrimHistory(history);
        SaveHistory(history);
        log.Info($"Tracked {login} / {rewardName} ({redemptionId}).");
        return true;
    }

    public bool OnUpdated()
    {
        CPH.TryGetArg("redemptionId", out string redemptionId);
        if (string.IsNullOrWhiteSpace(redemptionId))
            return false;
        var history = LoadHistory();
        int removed = history.RemoveAll(r => r.RedemptionId == redemptionId);
        if (removed > 0)
            SaveHistory(history);
        return removed > 0;
    }

    public bool Refund()
    {
        var log = Fc.Logger(CPH, Title, Version);
        var ev = Fc.CaptureEvent(CPH);
        if (IsBot(ev))
            return true;
        log.Init();
        if (!IsModOrBroadcaster(ev, out bool isBroadcaster))
        {
            Reply($"{Mention(ev)}, mods only.", ev);
            return false;
        }

        if (string.IsNullOrWhiteSpace(CommitTimerId))
        {
            Reply($"{Mention(ev)}, refund timer id is not set. Copy Timer Id into CommitTimerId (see SETUP.md).", ev);
            log.Error("CommitTimerId is empty.");
            return false;
        }

        CPH.TryGetArg("input0", out string input0);
        CPH.TryGetArg("input1", out string input1);
        if (string.IsNullOrWhiteSpace(input0))
        {
            Reply($"{Mention(ev)}, usage: {Usage}", ev);
            return false;
        }

        var settings = LoadSettings();
        int count = 1;
        if (!string.IsNullOrWhiteSpace(input1))
        {
            if (!int.TryParse(input1.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1)
            {
                Reply($"{Mention(ev)}, usage: {Usage}", ev);
                return false;
            }
        }

        count = Math.Min(count, settings.MaxCount);
        var login = input0.Replace("@", "").Trim();
        var userInfo = CPH.TwitchGetUserInfoByLogin(login);
        if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.UserId))
        {
            Reply($"{Mention(ev)}, '{login}' is not a Twitch user.", ev);
            return false;
        }

        if (string.Equals(ev.UserId, userInfo.UserId, StringComparison.Ordinal) && !isBroadcaster && !settings.ModSelfRefund)
        {
            Reply($"{Mention(ev)}, mods cannot refund themselves.", ev);
            return false;
        }

        if (LoadPending() != null)
        {
            Reply($"{Mention(ev)}, a refund is already queued. !refundcancel to abort it first.", ev);
            return false;
        }

        var history = LoadHistory();
        var picked = new List<RedemptionInfo>();
        for (int i = history.Count - 1; i >= 0 && picked.Count < count; i--)
        {
            if (history[i].UserId == userInfo.UserId)
                picked.Add(history[i]);
        }

        if (picked.Count == 0)
        {
            Reply($"{Mention(ev)}, no pending redemptions for @{userInfo.UserName}.", ev);
            return false;
        }

        int total = picked.Sum(p => p.RewardCost);
        var job = new PendingJob
        {
            QueuedByUserId = ev.UserId,
            QueuedByLogin = FirstNonEmpty(ev.User, ev.UserName),
            TargetUserId = userInfo.UserId,
            TargetLogin = userInfo.UserName,
            RedemptionIds = picked.Select(p => p.RedemptionId).ToList(),
        };
        SavePending(job);
        try
        {
            CPH.DisableTimerById(CommitTimerId);
            CPH.SetTimerInterval(CommitTimerId, settings.ConfirmDelaySeconds);
            CPH.EnableTimerById(CommitTimerId);
        }
        catch (Exception ex)
        {
            ClearPending();
            log.Error($"Failed to start commit timer: {ex.Message}");
            Reply($"{Mention(ev)}, could not start the refund timer.", ev);
            return false;
        }

        Reply(BuildQueuedMessage(ev, job, picked, total, settings), ev);
        log.Info($"Queued {picked.Count} refund(s) for {userInfo.UserName} ({total} points).");
        return true;
    }

    public bool Cancel()
    {
        var log = Fc.Logger(CPH, Title, Version);
        var ev = Fc.CaptureEvent(CPH);
        if (IsBot(ev))
            return true;
        log.Init();
        var job = LoadPending();
        if (job == null)
        {
            Reply($"{Mention(ev)}, there is no refund queued.", ev);
            return false;
        }

        IsModOrBroadcaster(ev, out bool isBroadcaster);
        bool isOwner = string.Equals(ev.UserId, job.QueuedByUserId, StringComparison.Ordinal);
        if (!isBroadcaster && !ev.IsModerator && !isOwner)
        {
            Reply($"{Mention(ev)}, you cannot cancel this refund.", ev);
            return false;
        }

        ClearPending();
        StopTimer();
        int n = job.RedemptionIds != null ? job.RedemptionIds.Count : 0;
        Reply($"{Mention(ev)}, cancelled the refund for @{job.TargetLogin} ({n} pending).", ev);
        log.Info($"Cancelled queued refund for {job.TargetLogin}.");
        return true;
    }

    public bool Commit()
    {
        var log = Fc.Logger(CPH, Title, Version);
        StopTimer();
        var job = LoadPending();
        ClearPending();
        if (job == null || job.RedemptionIds == null || job.RedemptionIds.Count == 0)
        {
            log.Info("Commit: no pending job.");
            return true;
        }

        var history = LoadHistory();
        var byId = history.Where(r => !string.IsNullOrEmpty(r.RedemptionId)).GroupBy(r => r.RedemptionId).ToDictionary(g => g.Key, g => g.First());
        int success = 0;
        int points = 0;
        var refunded = new List<RedemptionInfo>();
        foreach (var id in job.RedemptionIds)
        {
            if (!byId.TryGetValue(id, out var item))
                continue;
            bool ok = CPH.TwitchRedemptionCancel(item.RewardId, item.RedemptionId);
            if (!ok)
            {
                log.Warn($"TwitchRedemptionCancel failed for {item.RedemptionId}.");
                continue;
            }

            success++;
            points += item.RewardCost;
            refunded.Add(item);
            history.RemoveAll(r => r.RedemptionId == item.RedemptionId);
        }

        SaveHistory(history);
        if (success == 0)
        {
            CPH.SendMessage($"Refund for @{job.TargetLogin} failed (nothing left to cancel, or Twitch rejected it).");
            return false;
        }

        var settings = LoadSettings();
        var sb = new StringBuilder();
        sb.Append("Refunded ").Append(success).Append(success == 1 ? " reward" : " rewards").Append(" for @").Append(job.TargetLogin).Append(" (").Append(FormatPoints(points)).Append(" points).");
        if (settings.PrintRewards)
            AppendStackedLines(sb, refunded);
        CPH.SendMessage(sb.ToString());
        log.Info($"Committed {success} refund(s) for {job.TargetLogin} ({points} points).");
        return true;
    }

    private Settings LoadSettings()
    {
        int delay = Fc.GetSetting(CPH, Title, "confirm_delay", 10);
        int max = Fc.GetSetting(CPH, Title, "max_count", 25);
        return new Settings
        {
            ModSelfRefund = Fc.GetSetting(CPH, Title, "mod_self_refund", false),
            PrintRewards = Fc.GetSetting(CPH, Title, "print_rewards", true),
            ConfirmDelaySeconds = Clamp(delay, 1, 120),
            MaxCount = Clamp(max, 1, 100),
        };
    }

    private List<RedemptionInfo> LoadHistory()
    {
        string json = CPH.GetGlobalVar<string>(HistoryVar, true);
        if (string.IsNullOrWhiteSpace(json))
            return new List<RedemptionInfo>();
        try
        {
            return JsonConvert.DeserializeObject<List<RedemptionInfo>>(json) ?? new List<RedemptionInfo>();
        }
        catch
        {
            return new List<RedemptionInfo>();
        }
    }

    private void SaveHistory(List<RedemptionInfo> history)
    {
        CPH.SetGlobalVar(HistoryVar, JsonConvert.SerializeObject(history), true);
    }

    private static void TrimHistory(List<RedemptionInfo> history)
    {
        if (history.Count <= HistoryCap)
            return;
        history.RemoveRange(0, history.Count - HistoryCap);
    }

    private PendingJob LoadPending()
    {
        string json = CPH.GetGlobalVar<string>(PendingJobVar, false);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonConvert.DeserializeObject<PendingJob>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SavePending(PendingJob job)
    {
        CPH.SetGlobalVar(PendingJobVar, JsonConvert.SerializeObject(job), false);
    }

    private void ClearPending()
    {
        CPH.UnsetGlobalVar(PendingJobVar, false);
    }

    private void StopTimer()
    {
        if (string.IsNullOrWhiteSpace(CommitTimerId))
            return;
        try
        {
            CPH.DisableTimerById(CommitTimerId);
        }
        catch
        {
        // Timer missing or already disabled.
        }
    }

    private bool IsBot(EventContext ev)
    {
        if (string.Equals(ev.UserType, "bot", StringComparison.OrdinalIgnoreCase))
            return true;
        var bot = CPH.TwitchGetBot();
        if (bot == null)
            return false;
        if (!string.IsNullOrEmpty(bot.UserId) && bot.UserId == ev.UserId)
            return true;
        string name = FirstNonEmpty(ev.User, ev.UserName);
        return !string.IsNullOrEmpty(bot.UserName) && string.Equals(name, bot.UserName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsModOrBroadcaster(EventContext ev, out bool isBroadcaster)
    {
        isBroadcaster = false;
        var bc = CPH.TwitchGetBroadcaster();
        if (bc != null && !string.IsNullOrEmpty(bc.UserId) && bc.UserId == ev.UserId)
            isBroadcaster = true;
        return isBroadcaster || ev.IsModerator;
    }

    private string BuildQueuedMessage(EventContext ev, PendingJob job, List<RedemptionInfo> picked, int total, Settings settings)
    {
        var sb = new StringBuilder();
        sb.Append(Mention(ev)).Append(", refunding ").Append(picked.Count).Append(picked.Count == 1 ? " reward" : " rewards").Append(" for @").Append(job.TargetLogin).Append(" (").Append(FormatPoints(total)).Append(" points) in ").Append(settings.ConfirmDelaySeconds).Append("s. !refundcancel to abort.");
        if (settings.PrintRewards)
            AppendStackedLines(sb, picked);
        return sb.ToString();
    }

    private static void AppendStackedLines(StringBuilder sb, List<RedemptionInfo> items)
    {
        var stacks = items.GroupBy(i => i.RewardId ?? "").Select(g =>
        {
            var first = g.First();
            int n = g.Count();
            int sum = g.Sum(x => x.RewardCost);
            return new
            {
                Name = ShortName(first.RewardName),
                Count = n,
                Sum = sum
            };
        });
        foreach (var row in stacks)
        {
            sb.Append(" · ");
            sb.Append(row.Name);
            if (row.Count > 1)
                sb.Append(" ×").Append(row.Count);
            sb.Append(" (").Append(FormatPoints(row.Sum)).Append(")");
        }
    }

    private static string ShortName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "(unnamed)";
        if (name.Length <= 12)
            return name;
        return name.Substring(0, 5) + "..." + name.Substring(name.Length - 5);
    }

    private static string FormatPoints(int points)
    {
        return points.ToString("N0", CultureInfo.InvariantCulture);
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

    private static string Mention(EventContext ev)
    {
        return "@" + MessageTemplates.SanitizeMention(FirstNonEmpty(ev.User, ev.UserName));
    }

    private static string FirstNonEmpty(params string[] parts)
    {
        if (parts == null)
            return "";
        foreach (var p in parts)
        {
            if (!string.IsNullOrWhiteSpace(p))
                return p.Trim();
        }

        return "";
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private sealed class Settings
    {
        public bool ModSelfRefund;
        public bool PrintRewards;
        public int ConfirmDelaySeconds;
        public int MaxCount;
    }

    private sealed class RedemptionInfo
    {
        public string UserId { get; set; }
        public string UserLogin { get; set; }
        public string RewardId { get; set; }
        public string RedemptionId { get; set; }
        public string RewardName { get; set; }
        public int RewardCost { get; set; }
        public string RedeemedAt { get; set; }
    }

    private sealed class PendingJob
    {
        public string QueuedByUserId { get; set; }
        public string QueuedByLogin { get; set; }
        public string TargetUserId { get; set; }
        public string TargetLogin { get; set; }
        public List<string> RedemptionIds { get; set; }
    }
}