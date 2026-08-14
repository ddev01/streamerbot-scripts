using FluentConfig;
using FluentConfig.Runtime;
using System;
using System.Collections.Generic;

// Refs: FluentConfig.dll (Streamer.bot dlls/).
// Timer trigger and optional Twitch Stream Offline.
#if EXTERNAL_EDITOR
public class FiresaleEnd : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "Fire Sale";
    private const string Version = "1.0.0";
    private class Settings
    {
        public string TimerId { get; set; } = "";
        public bool SendMessage { get; set; } = true;
        public string MsgEnded { get; set; } = "✅ Fire sale ended! Restored {count} rewards to original prices.";
        public string MsgAutoEnded { get; set; } = "⏰ Fire sale ended automatically! Restored {count} rewards to original prices.";
    }

    private class RewardCost
    {
        public string RewardId { get; set; }
        public int Cost { get; set; }
    }

    private class SaleState
    {
        public bool Active { get; set; }
        public string GroupsLabel { get; set; } = "all rewards";
        public int RewardCount { get; set; }
        public List<RewardCost> OriginalCosts { get; set; }
    }

    public bool Execute()
    {
        var log = Fc.Logger(CPH, Title, Version);
        var settings = Fc.LoadSettings<Settings>(CPH, Title);
        var state = Fc.LoadData<SaleState>(CPH, Title);
        if (state == null || !state.Active)
        {
            DisableTimer(settings.TimerId);
            return true;
        }

        int restored = RestoreOriginalCosts(state, log);
        ClearState();
        DisableTimer(settings.TimerId);
        SetResultArgs(restored);
        log.Info($"Auto-end restored {restored} rewards.");
        Chat(settings, settings.MsgAutoEnded, restored, state.GroupsLabel);
        return true;
    }

    private void Chat(Settings settings, string template, int count, string groups)
    {
        if (!settings.SendMessage || string.IsNullOrWhiteSpace(template))
            return;
        CPH.SendMessage(Fc.ApplyTemplate(template, TemplateVars(count, groups)));
    }

    private static Dictionary<string, string> TemplateVars(int count, string groups)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["count"] = count.ToString(),
            ["groups"] = groups ?? "all rewards",
            ["mode"] = "",
            ["discount"] = "",
            ["multiplier"] = "",
            ["duration"] = "",
            ["remaining"] = "",
        };
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

    private void SetResultArgs(int restored)
    {
        CPH.SetArgument("firesaleActive", false);
        CPH.SetArgument("firesaleRestored", restored);
    }
}