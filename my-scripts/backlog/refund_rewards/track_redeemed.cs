//Trigger: Twitch reward redemption any.
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
        // Get args
        CPH.TryGetArg("rewardName", out string rewardName);
        CPH.TryGetArg("rewardId", out string rewardId);
        CPH.TryGetArg("redemptionId", out string redemptionId);
        CPH.TryGetArg("userId", out string userId);
        // Get existing history (as JSON string)
        string historyJson = CPH.GetGlobalVar<string>("rewardRedemptionHistory", true);
        List<RedemptionInfo> history;
        if (!string.IsNullOrEmpty(historyJson))
        {
            // Deserialize existing history
            history = JsonConvert.DeserializeObject<List<RedemptionInfo>>(historyJson);
        }
        else
        {
            // Start new history
            history = new List<RedemptionInfo>();
        }

        // Add new redemption
        history.Add(new RedemptionInfo { UserId = userId, RewardName = rewardName, RewardId = rewardId, RedemptionId = redemptionId, });
        // Serialize and save back to global var
        LogInfo($"Adding redemption to history: rewardName: {rewardName} - rewardId: {rewardId} - redemptionId: {redemptionId} - userId: {userId}");
        string updatedJson = JsonConvert.SerializeObject(history);
        LogInfo("Successfully added redemption to history");
        CPH.SetGlobalVar("rewardRedemptionHistory", updatedJson, true);
        return true;
    }

    private void LogInfo(string message)
    {
        CPH.LogInfo("[Refund Rewards] [#0] Track Rewards " + message);
    }

    private void LogError(string message)
    {
        CPH.LogError("[Refund Rewards] [#0] Track Rewards " + message);
    }
}