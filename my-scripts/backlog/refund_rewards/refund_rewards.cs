//Trigger: Command !refund [User] [Count].
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class RedemptionInfo
{
    public string UserId { get; set; }
    public string RewardName { get; set; }
    public string RewardId { get; set; }
    public string RedemptionId { get; set; }
}

public class CPHInline
{
    public bool Execute()
    {
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("rawInput", out string rawInput);
        CPH.TryGetArg("input0", out string input0);
        CPH.TryGetArg("input1", out string input1);
        // Validate input0 (username)
        if (string.IsNullOrEmpty(input0))
        {
            CPH.SendMessage($"@{user}, please enter a user to refund: '!refund [User] [Count]'");
            LogError($"'{user}' tried to refund with empty input0. RawInput: {rawInput}");
            return false;
        }

        // Validate input1 (count)
        int refundCount = 1;
        if (!string.IsNullOrEmpty(input1))
        {
            if (!int.TryParse(input1, out refundCount) || refundCount < 1)
            {
                CPH.SendMessage($"@{user}, please enter a valid number of redemptions to refund: '!refund [User] [Count]'");
                LogError($"'{user}' tried to refund with invalid input1. RawInput: {rawInput}");
                return false;
            }
        }

        string inputUser = input0.Replace("@", "").Trim();
        // Validate the user exists
        var userInfo = CPH.TwitchGetUserInfoByLogin(inputUser);
        if (userInfo == null)
        {
            CPH.SendMessage($"@{user}, the user '{inputUser}' does not exist.");
            LogError($"@{user}, the user '{inputUser}' does not exist.");
            return false;
        }

        string validUserId = userInfo.UserId;
        // Get redemption history
        string historyJson = CPH.GetGlobalVar<string>("rewardRedemptionHistory", true);
        List<RedemptionInfo> history = new List<RedemptionInfo>();
        if (!string.IsNullOrEmpty(historyJson))
        {
            history = JsonConvert.DeserializeObject<List<RedemptionInfo>>(historyJson);
        }

        // Find the last N redemptions for this user
        List<RedemptionInfo> toRefund = new List<RedemptionInfo>();
        for (int i = history.Count - 1; i >= 0 && toRefund.Count < refundCount; i--)
        {
            if (history[i].UserId == validUserId)
            {
                toRefund.Add(history[i]);
                history.RemoveAt(i);
            }
        }

        if (toRefund.Count == 0)
        {
            CPH.SendMessage($"@{user}, no redemption found for @{inputUser}.");
            LogError($"@{user}, no redemption found for @{inputUser}.");
            return false;
        }

        int successCount = 0;
        foreach (var redemption in toRefund)
        {
            bool result = CPH.TwitchRedemptionCancel(redemption.RewardId, redemption.RedemptionId);
            if (result)
            {
                successCount++;
                LogInfo($"Refunded @{inputUser}'s reward redemption (RewardId: {redemption.RewardId}, RedemptionId: {redemption.RedemptionId}).");
            }
            else
            {
                LogError($"Failed to refund @{inputUser}'s reward redemption (RewardId: {redemption.RewardId}, RedemptionId: {redemption.RedemptionId}).");
            }
        }

        // Save updated history
        LogInfo($"history: {historyJson}");
        string updatedJson = JsonConvert.SerializeObject(history);
        CPH.SetGlobalVar("rewardRedemptionHistory", updatedJson, true);
        if (successCount > 0)
        {
            CPH.SendMessage($"@{user}, refunded {successCount} redemption(s) for @{inputUser}.");
        }
        else
        {
            CPH.SendMessage($"@{user}, failed to refund any redemptions for @{inputUser}.");
        }

        return successCount > 0;
    }

    private void LogInfo(string message)
    {
        CPH.LogInfo("[Refund Rewards] [#2] Refund Reward " + message);
    }

    private void LogError(string message)
    {
        CPH.LogError("[Refund Rewards] [#2] Refund Reward " + message);
    }
}