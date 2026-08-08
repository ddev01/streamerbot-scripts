using System;
using System.Collections.Generic;
using Streamer.bot.Common.Events;

#if EXTERNAL_EDITOR
public class HotPotatoStart : CPHInlineBase
#else
public class CPHInline
#endif
{
    public string extensionName = "HOT POTATO";
    public string extensionVersion = "1.0.0";

    public bool Execute()
    {
        EventType event_type = CPH.GetEventType();
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("rewardId", out string rewardId);
        CPH.TryGetArg("redemptionId", out string redemptionId);
        CPH.TryGetArg("timeToPass", out int timeToPass);
        string passRewardId = CPH.GetGlobalVar<string?>("passRewardId", true) ?? null;
        if (CPH.GetGlobalVar<bool>("hotPotatoGameActive", false))
        {
            CPH.SendMessage($"@{user}, there's already a game of Hot Potato going on.");
            if (event_type == EventType.TwitchRewardRedemption)
            {
                CPH.TwitchRedemptionCancel(rewardId, redemptionId);
            }

            PostToLog($"{user} attempted to start a game while another one was already active.");
            return false;
        }

        int minChatters = CPH.TryGetArg("minimumActiveChatters", out long temp1) ? (int)temp1 : 3;
        var hotPotato_isActive_List = CPH.GetTwitchUsersVar<DateTime>("hotPotato_isActive", false);
        int activeChatters = 0;
        List<string> activeUsers = new List<string>();
        foreach (var entry in hotPotato_isActive_List)
        {
            if ((DateTime.Now - entry.Value).TotalMinutes <= 5)
            {
                activeChatters++;
                activeUsers.Add(entry.UserId);
            }
        }

        if (activeChatters < minChatters)
        {
            CPH.SendMessage(
                $"Sorry, @{user}, there are not enough active chatters to start a game of Hot Potato right now ({activeChatters}/{minChatters}). 🥔 "
            );
            if (event_type == EventType.TwitchRewardRedemption)
            {
                CPH.TwitchRedemptionCancel(rewardId, redemptionId);
            }

            PostToLog(
                $"{user} attempted to start a game while not enough chatters were available ({activeChatters}/{minChatters})"
            );
            return false;
        }
        else
        {
            CPH.SetGlobalVar("hotPotato_isActive_Filtered", activeUsers, false);
        }

        Random random = new Random();
        string randomUserId = activeUsers[random.Next(activeUsers.Count)];
        CPH.SetGlobalVar("hotPotatoGameActive", true, false);
        CPH.SetGlobalVar("hotPotatoCarrier", randomUserId, false);
        var randomUserInfo = CPH.TwitchGetUserInfoById(randomUserId);
        string randomUser = randomUserInfo.UserName;
        int hotPotatoesCaught =
            CPH.GetTwitchUserVarById<int?>(randomUserId, "hotPotatoesCaught", true) ?? 0;
        int hotPotatoesPassed =
            CPH.GetTwitchUserVarById<int?>(userId, "hotPotatoesPassed", true) ?? 0;
        CPH.SetTwitchUserVarById(randomUserId, "hotPotatoesCaught", hotPotatoesCaught + 1, true);
        CPH.SetTwitchUserVarById(userId, "hotPotatoesPassed", hotPotatoesPassed + 1, true);
        int hotPotatoesPassed_current =
            CPH.GetGlobalVar<int?>("hotPotatoesPassed_current", false) ?? 0;
        int hotPotatoesPassed_total = CPH.GetGlobalVar<int?>("hotPotatoesPassed_total", true) ?? 0;
        CPH.SetGlobalVar("hotPotatoesPassed_current", hotPotatoesPassed_current + 1, false);
        CPH.SetGlobalVar("hotPotatoesPassed_total", hotPotatoesPassed_total + 1, true);
        CPH.SetTimerInterval("5c6059cd-9848-4fee-9f94-dfc467927a4b", timeToPass);
        CPH.EnableTimerById("5c6059cd-9848-4fee-9f94-dfc467927a4b");
        string message = "Type '!pass' into chat";
        if (event_type == EventType.TwitchRewardRedemption)
        {
            message = "Use the \"Pass the Potato\" channel point reward";
            CPH.DisableReward(rewardId);
            CPH.EnableReward(passRewardId);
        }

        CPH.SendMessage(
            $"@{user}, has started a game of hot potato. The first hot potato lands in @{randomUser}'s hand. {message} to pass the hot potato to the next person before it burns your hands! 🥔 🔥"
        );
        PostToLog($"{user} started a game with {activeChatters} participants.");
        return true;
    }

    public void PostToLog(string log)
    {
        CPH.LogInfo($"[{extensionName} v.{extensionVersion}] {log}");
    }
}
