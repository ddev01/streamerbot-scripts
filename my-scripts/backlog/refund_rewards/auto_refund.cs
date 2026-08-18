//Trigger: twitch reward redeemed.
using System;
using System.Collections.Generic;

public class CPHInline
{
    public bool Execute()
    {
        return true;
    }

    private static readonly StringComparer CI = StringComparer.OrdinalIgnoreCase;

    private static HashSet<string> Set(params string[] users)
    {
        var hs = new HashSet<string>(CI);
        if (users != null)
        {
            for (int i = 0; i < users.Length; i++)
            {
                var u = users[i];
                if (!string.IsNullOrWhiteSpace(u))
                    hs.Add(u.Trim().ToLowerInvariant());
            }
        }

        return hs;
    }

    private static readonly Dictionary<string, HashSet<string>> rewardOwners = new Dictionary<
        string,
        HashSet<string>
    >(CI)
    {
        { "hewwo pwincess", Set("lalunaay ") },
        { "Wiwiwiwi Cat", Set("nokillskiera") },
        { "Why Are You Running", Set("nokillskiera") },
        { "Damn Son", Set("vta_d3mon") },
    };

    private static string normalizeName(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;
        return string.Join(
            " ",
            s.Replace("[MP3]", "")
                .Replace("[Fortnite]", "")
                .Replace("@", "")
                .Trim()
                .ToLowerInvariant()
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
        );
    }

    public bool Refund()
    {
        CPH.TryGetArg("rewardName", out string rewardName);
        CPH.TryGetArg("rewardId", out string rewardId);
        CPH.TryGetArg("redemptionId", out string redemptionId);
        CPH.TryGetArg("userId", out string userId);
        CPH.TryGetArg("user", out string user);
        if (string.IsNullOrWhiteSpace(rewardId) || string.IsNullOrWhiteSpace(redemptionId))
        {
            CPH.LogError("[AutoFulfill] Missing rewardId or redemptionId.");
            return false;
        }

        string baseRewardName = normalizeName(rewardName);
        bool shouldReject = false;
        if (
            !string.IsNullOrEmpty(baseRewardName)
            && rewardOwners.TryGetValue(baseRewardName, out var ownerUsers)
        )
        {
            // Compare usernames case-insensitively and without leading @
            string normalizedUser = normalizeName(user);
            if (!string.IsNullOrEmpty(normalizedUser) && ownerUsers.Contains(normalizedUser))
            {
                shouldReject = true;
            }
        }

        bool success;
        if (shouldReject)
        {
            success = CPH.TwitchRedemptionCancel(rewardId, redemptionId);
            CPH.LogInfo($"[AutoFulfill] {user} owns {baseRewardName}. refunded_status={success}");
        }
        else
        {
            success = CPH.TwitchRedemptionFulfill(rewardId, redemptionId);
            CPH.LogInfo(
                $"[AutoFulfill] {user} does not own {baseRewardName}. mark_as_completed_status={success}"
            );
        }

        return success;
    }
}
