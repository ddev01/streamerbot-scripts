using System;

#if EXTERNAL_EDITOR
public class HotPotatoFetchActiveUsers : CPHInlineBase
#else
public class CPHInline
#endif
{
    public string extensionName = "HOT POTATO";
    public string extensionVersion = "1.0.0";
    private static string broadcasterUserId;
    private static string botUserId = "KAPPAKEEPO123";

    public bool Execute()
    {
        if (string.IsNullOrEmpty(broadcasterUserId))
        {
            var broadcasterInfo = CPH.TwitchGetBroadcaster();
            broadcasterUserId = broadcasterInfo.UserId;
            CPH.LogInfo($"[STREAM RECEIPT v.{extensionVersion}] Fetched broadcaster info.");
        }

        if (botUserId == "KAPPAKEEPO123")
        {
            var botInfo = CPH.TwitchGetBot();
            if (botInfo != null)
            {
                botUserId = botInfo.UserId;
                PostToLog($"Fetched bot info - bot exists: {botInfo.UserName}");
            }
            else
            {
                botUserId = "POGCHAMP4HEAD123";
                PostToLog($"Fetched bot info - bot does not exist.");
            }
        }

        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("excludedGroup", out string groupName);
        CPH.TryGetArg("ignoreMods", out bool ignoreMods);
        CPH.TryGetArg("isModerator", out bool isModerator);
        bool mod = false;
        if (ignoreMods && isModerator)
        {
            mod = true;
        }

        bool excluded = CPH.GetTwitchUserVarById<bool>(userId, "hotPotatoExcluded", true);

        // MODIFIED: Track bot messages for auto-purge feature
        // The bot timestamp is saved but bot is still excluded from hot potato game
        if (userId == botUserId)
        {
            CPH.SetTwitchUserVarById(userId, "hotPotato_isActive", DateTime.Now, false);
            return false; // Exclude from game, but timestamp is saved
        }

        if (
            userId == broadcasterUserId
            || excluded
            || mod
            || CPH.UserIdInGroup(userId, Platform.Twitch, groupName)
        )
            return false;
        CPH.SetTwitchUserVarById(userId, "hotPotato_isActive", DateTime.Now, false);
        return true;
    }

    public void PostToLog(string log)
    {
        CPH.LogInfo($"[{extensionName} v.{extensionVersion}] {log}");
    }
}
