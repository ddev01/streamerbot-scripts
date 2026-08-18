//Trigger: Twitch reward redemption updated.
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
        CPH.TryGetArg("rewardName", out string rewardName);
        CPH.TryGetArg("rewardId", out string rewardId);
        CPH.TryGetArg("redemptionId", out string redemptionId);
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("rewardStatus", out string rewardStatus);
        string historyJson = CPH.GetGlobalVar<string>("rewardRedemptionHistory", true);
        List<RedemptionInfo> history;
        if (!string.IsNullOrEmpty(historyJson))
        {
            // Deserialize existing history
            history = JsonConvert.DeserializeObject<List<RedemptionInfo>>(historyJson);
        }
        else
        {
            //stop execution
            LogError($": {rewardName} - redeemed by: {user} - updated status to: {rewardStatus} - but no history found in global var: rewardRedemptionHistory - rewardId: {rewardId} - redemptionId: {redemptionId} - userId: {userId}");
            return false;
        }

        // Find the redemption by RedemptionId
        RedemptionInfo redemption = null;
        foreach (var r in history)
        {
            if (r.RedemptionId == redemptionId)
            {
                redemption = r;
                break;
            }
        }

        if (redemption == null)
        {
            LogError($": {rewardName} - redeemed by: {user} - updated status to: {rewardStatus} - but redemption not found in history - rewardId: {rewardId} - redemptionId: {redemptionId} - userId: {userId}");
            return false;
        }

        // Remove the redemption from history (RedemptionId should be unique)
        history.Remove(redemption);
        LogInfo($": {rewardName} - removed redemption from history - redeemed by: {user} - updated status to: {rewardStatus} - rewardId: {rewardId} - redemptionId: {redemptionId} - userId: {userId}");
        string updatedJson = JsonConvert.SerializeObject(history);
        LogInfo(": Successfully removed redemption from history");
        CPH.SetGlobalVar("rewardRedemptionHistory", updatedJson, true);
        return true;
    }

    private void LogInfo(string message)
    {
        CPH.LogInfo("[Refund Rewards] [#1] Track Reward Redemption Updated" + message);
    }

    private void LogError(string message)
    {
        CPH.LogError("[Refund Rewards] [#1] Track Reward Redemption Updated" + message);
    }
}