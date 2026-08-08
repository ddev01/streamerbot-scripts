using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Streamer.bot.Plugin.Interface.Model;

public class RewardCostInfo
{
    public string RewardId { get; set; }
    public int Cost { get; set; }
}

public class FireSaleParams
{
    public bool IsFree { get; set; }
    public bool IsRandom { get; set; }
    public double Multiplier { get; set; }
    public double RandomMin { get; set; }
    public double RandomMax { get; set; }
    public string Duration { get; set; }
    public string RewardGroups { get; set; }
    public string ExcludeGroups { get; set; }
}

#if EXTERNAL_EDITOR
public class FiresaleMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    // Global variable keys (constants for type safety and maintainability)
    private const string KEY_ACTIVE = "fireSaleActive";
    private const string KEY_IS_RANDOM = "fireSaleIsRandom";
    private const string KEY_IS_FREE = "fireSaleIsFree";
    private const string KEY_MULTIPLIER = "fireSaleMultiplier";
    private const string KEY_RANDOM_MIN = "fireSaleRandomMin";
    private const string KEY_RANDOM_MAX = "fireSaleRandomMax";
    private const string KEY_START_TIME = "fireSaleStartTime";
    private const string KEY_END_TIME = "fireSaleEndTime";
    private const string KEY_REWARD_GROUPS = "fireSaleRewardGroups";
    private const string KEY_EXCLUDE_GROUPS = "fireSaleExcludeGroups";
    private const string KEY_ORIGINAL_COSTS = "fireSale_originalCosts";

    // Timer ID for auto-ending fire sale (replace with actual Timer ID from Streamer.bot)
    private const string TIMER_ID_END_SALE = "d50840a9-696e-40f8-b89d-f712ecb5027f";

    // DateTime format for consistent serialization
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";

    public bool Execute()
    {
        // Get command inputs
        CPH.TryGetArg("input0", out string input0);
        string command = input0?.ToLower();

        // Route based on first argument
        if (command == "status")
        {
            return ShowStatus();
        }
        else if (command == "off" || command == "reset")
        {
            return ResetFireSale();
        }
        else
        {
            // Handle fire sale activation
            return HandleFireSaleActivation();
        }
    }

    // PUBLIC: Can be called from stream start/end actions
    public bool ResetFireSale()
    {
        // Simplified guard clause - inline check
        if (!CPH.GetGlobalVar<bool>(KEY_ACTIVE, true))
        {
            return true; // Already off, nothing to reset
        }

        // IMMEDIATE FEEDBACK: Send processing message before the slow restore
        CPH.SendMessage("🔥 Fire sale ending! Restoring original reward prices...");

        // Disable auto-end timer if it was running
        CPH.DisableTimerById(TIMER_ID_END_SALE);

        int restoredCount = RestoreOriginalCosts();
        ClearFireSaleState();

        // Send completion message
        CPH.SendMessage($"✅ Fire sale ended! Restored {restoredCount} rewards to original prices.");

        return true;
    }

    // PUBLIC: Called by timer action when duration expires
    public bool TimerEndSale()
    {
        // Check if fire sale is still active (safety check)
        bool isActive = CPH.GetGlobalVar<bool>(KEY_ACTIVE, true);
        if (!isActive)
        {
            return true; // Already ended, nothing to do
        }

        // Reset the fire sale
        int restoredCount = RestoreOriginalCosts();
        ClearFireSaleState();

        // Send message indicating auto-end
        CPH.SendMessage(
            $"⏰ Fire sale has ended automatically! Restored {restoredCount} rewards to original prices."
        );

        return true;
    }

    // PUBLIC: Can be called from timer actions
    public bool ShowStatus()
    {
        bool isActive = CPH.GetGlobalVar<bool>(KEY_ACTIVE, true);

        if (!isActive)
        {
            CPH.SendMessage("🔥 Fire sale is currently OFF.");
            return true;
        }

        // Read state variables
        bool isRandom = CPH.GetGlobalVar<bool>(KEY_IS_RANDOM, true);
        bool isFree = CPH.GetGlobalVar<bool>(KEY_IS_FREE, true);
        double multiplier = CPH.GetGlobalVar<double>(KEY_MULTIPLIER, true);
        double randomMin = CPH.GetGlobalVar<double>(KEY_RANDOM_MIN, true);
        double randomMax = CPH.GetGlobalVar<double>(KEY_RANDOM_MAX, true);
        string endTimeStr = CPH.GetGlobalVar<string>(KEY_END_TIME, true);
        string rewardGroups = CPH.GetGlobalVar<string>(KEY_REWARD_GROUPS, true);
        string excludeGroups = CPH.GetGlobalVar<string>(KEY_EXCLUDE_GROUPS, true);

        // Calculate reward count
        int rewardCount = CalculateAffectedRewards(rewardGroups, excludeGroups);

        // Build and send status message
        string message = BuildStatusMessage(
            isRandom,
            isFree,
            multiplier,
            randomMin,
            randomMax,
            rewardGroups,
            rewardCount,
            endTimeStr
        );
        CPH.SendMessage(message);

        return true;
    }

    private bool HandleFireSaleActivation()
    {
        // Read preset arguments from Streamerbot Set Arguments (all come as strings)
        CPH.TryGetArg("rewardGroups", out string rewardGroupsRaw);
        CPH.TryGetArg("excludeGroups", out string excludeGroupsRaw);
        CPH.TryGetArg("defaultMultiplier", out string defaultMultiplierStr);
        CPH.TryGetArg("randomEnabled", out string randomEnabledStr);
        CPH.TryGetArg("randomMin", out string randomMinStr);
        CPH.TryGetArg("randomMax", out string randomMaxStr);

        // Parse and sanitize string arguments (treat space/"none"/"null" as empty)
        string rewardGroups = NormalizeEmptyValue(rewardGroupsRaw);
        string excludeGroups = NormalizeEmptyValue(excludeGroupsRaw);

        // Parse boolean with validation
        bool randomEnabled = ParseBooleanArg(randomEnabledStr, false);

        // Parse numeric arguments with validation and defaults
        double defaultMultiplier = ParseMultiplierArg(defaultMultiplierStr, 0.5);
        double randomMin = ParseMultiplierArg(randomMinStr, 0.1);
        double randomMax = ParseMultiplierArg(randomMaxStr, 0.9);

        // Validate random range
        if (randomMin >= randomMax)
        {
            CPH.LogWarn(
                $"Invalid random range: min ({randomMin}) >= max ({randomMax}). Using defaults."
            );
            randomMin = 0.1;
            randomMax = 0.9;
        }

        // Get command inputs
        CPH.TryGetArg("input0", out string input0);
        CPH.TryGetArg("input1", out string input1);
        CPH.TryGetArg("input2", out string input2);

        // Parse inputs
        var fireParams = ParseInputs(
            input0,
            input1,
            input2,
            rewardGroups,
            excludeGroups,
            defaultMultiplier,
            randomEnabled,
            randomMin,
            randomMax
        );

        // Get all rewards
        List<TwitchReward> rewards = CPH.TwitchGetRewards();
        if (rewards == null || rewards.Count == 0)
        {
            CPH.SendMessage("No rewards found.");
            return false;
        }

        return HandleTurnOn(rewards, fireParams);
    }

    // Helper: Normalize empty values (space, "none", "null" -> empty string)
    private string NormalizeEmptyValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string trimmed = value.Trim().ToLower();
        if (trimmed == "none" || trimmed == "null")
            return "";

        return value.Trim();
    }

    // Helper: Parse boolean argument (accepts "true", "1", "yes")
    private bool ParseBooleanArg(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        string lower = value.Trim().ToLower();
        if (lower == "true" || lower == "1" || lower == "yes" || lower == "on")
            return true;
        if (lower == "false" || lower == "0" || lower == "no" || lower == "off")
            return false;

        CPH.LogWarn($"Invalid boolean value '{value}'. Using default: {defaultValue}");
        return defaultValue;
    }

    // Helper: Parse multiplier argument with validation
    private double ParseMultiplierArg(string value, double defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (double.TryParse(value.Trim(), out double result))
        {
            // Validate range (0.01 to 1.0)
            if (result >= 0.01 && result <= 1.0)
                return result;

            CPH.LogWarn(
                $"Multiplier {result} out of range (0.01-1.0). Using default: {defaultValue}"
            );
            return defaultValue;
        }

        CPH.LogWarn($"Invalid number '{value}'. Using default: {defaultValue}");
        return defaultValue;
    }

    private FireSaleParams ParseInputs(
        string input0,
        string input1,
        string input2,
        string rewardGroups,
        string excludeGroups,
        double defaultMultiplier,
        bool randomEnabled,
        double randomMin,
        double randomMax
    )
    {
        var result = new FireSaleParams
        {
            RewardGroups = rewardGroups ?? "",
            ExcludeGroups = excludeGroups ?? "",
            RandomMin = randomMin,
            RandomMax = randomMax,
        };

        // Note: "off", "reset", and "status" are handled in Execute() routing

        // Handle "free" command
        if (input0?.ToLower() == "free")
        {
            result.IsFree = true;
            result.Duration = IsDuration(input1) ? input1 : null;
            return result;
        }

        // Handle "random" command
        if (input0?.ToLower() == "random")
        {
            result.IsRandom = true;

            // Check if input1 is a range (e.g., "0.1-0.9")
            if (IsRange(input1))
            {
                var (min, max) = ParseRange(input1);
                result.RandomMin = min;
                result.RandomMax = max;
                result.Duration = IsDuration(input2) ? input2 : null;
            }
            else if (IsDuration(input1))
            {
                result.Duration = input1;
            }

            return result;
        }

        // Handle number (multiplier)
        if (IsNumber(input0))
        {
            result.Multiplier = double.Parse(input0);
            result.Duration = IsDuration(input1) ? input1 : null;
            return result;
        }

        // Handle just duration
        if (IsDuration(input0))
        {
            result.IsRandom = randomEnabled;
            result.Multiplier = randomEnabled ? 0 : defaultMultiplier;
            result.Duration = input0;
            return result;
        }

        // Default: use preset settings
        result.IsRandom = randomEnabled;
        result.Multiplier = randomEnabled ? 0 : defaultMultiplier;
        return result;
    }

    private bool IsNumber(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;
        return double.TryParse(input, out double result) && result > 0 && result <= 1;
    }

    private bool IsDuration(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;
        string lower = input.ToLower();
        return lower.EndsWith("m")
            || lower.EndsWith("h")
            || lower.EndsWith("min")
            || lower.EndsWith("hour")
            || lower.EndsWith("mins")
            || lower.EndsWith("hours");
    }

    private bool IsRange(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;
        if (!input.Contains("-"))
            return false;
        var parts = input.Split('-');
        return parts.Length == 2 && IsNumber(parts[0]) && IsNumber(parts[1]);
    }

    private (double min, double max) ParseRange(string range)
    {
        var parts = range.Split('-');
        double min = double.Parse(parts[0]);
        double max = double.Parse(parts[1]);
        return (min, max);
    }

    private int ParseDurationToMinutes(string duration)
    {
        if (string.IsNullOrEmpty(duration))
            return 0;

        string lower = duration.ToLower();
        string numberPart = lower;

        if (lower.EndsWith("hours") || lower.EndsWith("hour"))
        {
            numberPart = lower.Replace("hours", "").Replace("hour", "").Trim();
        }
        else if (lower.EndsWith("mins") || lower.EndsWith("min"))
        {
            numberPart = lower.Replace("mins", "").Replace("min", "").Trim();
        }
        else if (lower.EndsWith("h"))
        {
            numberPart = lower.Replace("h", "").Trim();
        }
        else if (lower.EndsWith("m"))
        {
            numberPart = lower.Replace("m", "").Trim();
        }

        if (double.TryParse(numberPart, out double value))
        {
            if (lower.Contains("h"))
                return (int)(value * 60);
            else
                return (int)value;
        }

        return 0;
    }

    // Helper method to calculate discount percentage from multiplier
    private int GetDiscountPercent(double multiplier)
    {
        return (int)Math.Round((1 - multiplier) * 100);
    }

    private bool HandleTurnOn(List<TwitchReward> rewards, FireSaleParams fireParams)
    {
        // Check if already active
        bool isActive = CPH.GetGlobalVar<bool>(KEY_ACTIVE, true);
        if (isActive)
        {
            CPH.SendMessage(
                "🔥 Fire sale is already active! Use '!firesale off' to turn it off first."
            );
            return false;
        }

        // Filter rewards
        var filteredRewards = FilterRewards(
            rewards,
            fireParams.RewardGroups,
            fireParams.ExcludeGroups
        );
        if (filteredRewards.Count == 0)
        {
            CPH.SendMessage($"No rewards found for the specified groups.");
            return false;
        }

        // IMMEDIATE FEEDBACK: Send "processing" message before the slow operations
        SendProcessingMessage(fireParams, filteredRewards.Count);

        // Save original costs
        SaveOriginalCosts(filteredRewards);

        // Apply discount/free/random (THIS IS THE SLOW PART - ~10 seconds for 20 rewards)
        double actualMultiplier = 0;
        if (fireParams.IsFree)
        {
            ApplyFreeMode(filteredRewards);
        }
        else if (fireParams.IsRandom)
        {
            actualMultiplier = ApplyRandomDiscount(
                filteredRewards,
                fireParams.RandomMin,
                fireParams.RandomMax
            );
        }
        else
        {
            actualMultiplier = fireParams.Multiplier;
            ApplyDiscount(filteredRewards, fireParams.Multiplier);
        }

        // Update state
        UpdateFireSaleState(fireParams, actualMultiplier);

        // Send completion message
        SendCompletionMessage(fireParams, filteredRewards.Count, actualMultiplier);

        return true;
    }

    private int CalculateAffectedRewards(string rewardGroups, string excludeGroups)
    {
        List<TwitchReward> rewards = CPH.TwitchGetRewards();
        if (rewards == null)
            return 0;

        // Reuse FilterRewards logic to avoid duplication (DRY principle)
        var filtered = FilterRewards(rewards, rewardGroups, excludeGroups);
        return filtered.Count;
    }

    private string BuildStatusMessage(
        bool isRandom,
        bool isFree,
        double multiplier,
        double randomMin,
        double randomMax,
        string rewardGroups,
        int rewardCount,
        string endTimeStr
    )
    {
        string groupText = string.IsNullOrEmpty(rewardGroups) ? "all rewards" : rewardGroups;
        string message = "🔥 Fire sale is ACTIVE! ";

        if (isFree)
        {
            message += $"FREE (1 point) on {groupText} ({rewardCount} rewards)";
        }
        else if (isRandom)
        {
            int minPercent = GetDiscountPercent(randomMax);
            int maxPercent = GetDiscountPercent(randomMin);
            message +=
                $"RANDOM ({minPercent}%-{maxPercent}%) discount on {groupText} ({rewardCount} rewards)";
        }
        else
        {
            int discountPercent = GetDiscountPercent(multiplier);
            message += $"{discountPercent}% discount on {groupText} ({rewardCount} rewards)";
        }

        // Add time remaining (with culture-safe DateTime parsing)
        if (
            !string.IsNullOrEmpty(endTimeStr)
            && DateTime.TryParseExact(
                endTimeStr,
                DATE_FORMAT,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime endTime
            )
        )
        {
            TimeSpan remaining = endTime - DateTime.Now;
            if (remaining.TotalMinutes > 0)
            {
                int remainingMinutes = (int)Math.Ceiling(remaining.TotalMinutes);
                message += $". Time remaining: {remainingMinutes} minutes";
            }
        }

        return message;
    }

    private List<TwitchReward> FilterRewards(
        List<TwitchReward> rewards,
        string rewardGroups,
        string excludeGroups
    )
    {
        // Parse exclude groups (has higher priority)
        var excludeList = new List<string>();
        if (!string.IsNullOrWhiteSpace(excludeGroups))
        {
            string[] excludeParts = excludeGroups.Split(',');
            foreach (string part in excludeParts)
            {
                // Trim, convert to lowercase, and handle empty string as "no group"
                string normalized = part.Trim().ToLower();
                if (!string.IsNullOrEmpty(normalized))
                {
                    excludeList.Add(normalized);
                }
            }
        }

        // Parse include groups
        List<TwitchReward> filteredRewards;
        if (string.IsNullOrWhiteSpace(rewardGroups))
        {
            // Include all rewards if no specific groups are specified
            filteredRewards = new List<TwitchReward>(rewards);
        }
        else
        {
            // Include only specified groups
            var includeList = new List<string>();
            string[] includeParts = rewardGroups.Split(',');
            foreach (string part in includeParts)
            {
                // Trim, convert to lowercase, and handle empty string as "no group"
                string normalized = part.Trim().ToLower();
                if (!string.IsNullOrEmpty(normalized))
                {
                    includeList.Add(normalized);
                }
            }

            // Filter rewards by include list (case-insensitive, space-stripped)
            filteredRewards = new List<TwitchReward>();
            foreach (var reward in rewards)
            {
                // Normalize reward group: trim spaces, lowercase
                string rewardGroup = (reward.Group ?? "").Trim().ToLower();
                if (includeList.Contains(rewardGroup))
                {
                    filteredRewards.Add(reward);
                }
            }
        }

        // Exclude specified groups (higher priority - even if in include list, case-insensitive, space-stripped)
        if (excludeList.Count > 0)
        {
            var finalRewards = new List<TwitchReward>();
            foreach (var reward in filteredRewards)
            {
                // Normalize reward group: trim spaces, lowercase
                string rewardGroup = (reward.Group ?? "").Trim().ToLower();
                if (!excludeList.Contains(rewardGroup))
                {
                    finalRewards.Add(reward);
                }
            }
            return finalRewards;
        }

        return filteredRewards;
    }

    private void SaveOriginalCosts(List<TwitchReward> rewards)
    {
        var rewardCosts = new List<RewardCostInfo>();
        foreach (var reward in rewards)
        {
            rewardCosts.Add(new RewardCostInfo { RewardId = reward.Id, Cost = reward.Cost });
        }

        string json = JsonConvert.SerializeObject(rewardCosts);
        CPH.SetGlobalVar(KEY_ORIGINAL_COSTS, json, true);
    }

    private int RestoreOriginalCosts()
    {
        string json = CPH.GetGlobalVar<string>(KEY_ORIGINAL_COSTS, true);
        if (string.IsNullOrEmpty(json))
        {
            return 0;
        }

        var rewardCosts = JsonConvert.DeserializeObject<List<RewardCostInfo>>(json);

        // Null check after deserialization (safety)
        if (rewardCosts == null)
        {
            return 0;
        }

        int restoredCount = 0;
        foreach (var costInfo in rewardCosts)
        {
            CPH.UpdateRewardCost(costInfo.RewardId, costInfo.Cost);
            restoredCount++;
        }

        // Use UnsetGlobalVar for proper cleanup
        CPH.UnsetGlobalVar(KEY_ORIGINAL_COSTS, true);
        return restoredCount;
    }

    private void ApplyDiscount(List<TwitchReward> rewards, double multiplier)
    {
        foreach (var reward in rewards)
        {
            int newCost = Math.Max(1, (int)Math.Round(reward.Cost * multiplier));
            CPH.UpdateRewardCost(reward.Id, newCost);
        }
    }

    private void ApplyFreeMode(List<TwitchReward> rewards)
    {
        foreach (var reward in rewards)
        {
            CPH.UpdateRewardCost(reward.Id, 1);
        }
    }

    private double ApplyRandomDiscount(List<TwitchReward> rewards, double min, double max)
    {
        Random random = new Random();
        double randomMultiplier = random.NextDouble() * (max - min) + min;

        foreach (var reward in rewards)
        {
            int newCost = Math.Max(1, (int)Math.Round(reward.Cost * randomMultiplier));
            CPH.UpdateRewardCost(reward.Id, newCost);
        }

        return randomMultiplier;
    }

    private void UpdateFireSaleState(FireSaleParams fireParams, double actualMultiplier)
    {
        CPH.SetGlobalVar(KEY_ACTIVE, true, true);
        CPH.SetGlobalVar(KEY_IS_RANDOM, fireParams.IsRandom, true);
        CPH.SetGlobalVar(KEY_IS_FREE, fireParams.IsFree, true);
        CPH.SetGlobalVar(KEY_MULTIPLIER, actualMultiplier, true);
        CPH.SetGlobalVar(KEY_RANDOM_MIN, fireParams.RandomMin, true);
        CPH.SetGlobalVar(KEY_RANDOM_MAX, fireParams.RandomMax, true);
        CPH.SetGlobalVar(KEY_START_TIME, DateTime.Now.ToString(DATE_FORMAT), true);
        CPH.SetGlobalVar(KEY_REWARD_GROUPS, fireParams.RewardGroups, true);
        CPH.SetGlobalVar(KEY_EXCLUDE_GROUPS, fireParams.ExcludeGroups, true);

        // Calculate end time if duration specified
        if (!string.IsNullOrEmpty(fireParams.Duration))
        {
            int minutes = ParseDurationToMinutes(fireParams.Duration);
            if (minutes > 0)
            {
                DateTime endTime = DateTime.Now.AddMinutes(minutes);
                CPH.SetGlobalVar(KEY_END_TIME, endTime.ToString(DATE_FORMAT), true);

                // Enable and configure timer for auto-end
                int durationInSeconds = minutes * 60;
                CPH.EnableTimerById(TIMER_ID_END_SALE);
                CPH.SetTimerInterval(TIMER_ID_END_SALE, durationInSeconds);
            }
            else
            {
                CPH.SetGlobalVar(KEY_END_TIME, "", true);
                // Disable timer if duration parsing failed
                CPH.DisableTimerById(TIMER_ID_END_SALE);
            }
        }
        else
        {
            CPH.SetGlobalVar(KEY_END_TIME, "", true);
            // Disable timer if no duration specified
            CPH.DisableTimerById(TIMER_ID_END_SALE);
        }
    }

    private void ClearFireSaleState()
    {
        CPH.SetGlobalVar(KEY_ACTIVE, false, true);
        CPH.SetGlobalVar(KEY_IS_RANDOM, false, true);
        CPH.SetGlobalVar(KEY_IS_FREE, false, true);
        CPH.SetGlobalVar(KEY_MULTIPLIER, 0, true);
        CPH.SetGlobalVar(KEY_RANDOM_MIN, 0, true);
        CPH.SetGlobalVar(KEY_RANDOM_MAX, 0, true);
        CPH.SetGlobalVar(KEY_START_TIME, "", true);
        CPH.SetGlobalVar(KEY_END_TIME, "", true);
        CPH.SetGlobalVar(KEY_REWARD_GROUPS, "", true);
        CPH.SetGlobalVar(KEY_EXCLUDE_GROUPS, "", true);
    }

    private void SendProcessingMessage(FireSaleParams fireParams, int rewardCount)
    {
        string groupText = string.IsNullOrEmpty(fireParams.RewardGroups)
            ? "all rewards"
            : fireParams.RewardGroups;
        string message = "🔥 Fire sale activating! ";

        if (fireParams.IsFree)
        {
            message += $"Setting {rewardCount} rewards to FREE (1 point)...";
        }
        else if (fireParams.IsRandom)
        {
            int minPercent = GetDiscountPercent(fireParams.RandomMax);
            int maxPercent = GetDiscountPercent(fireParams.RandomMin);
            message +=
                $"Applying RANDOM ({minPercent}%-{maxPercent}%) discount to {rewardCount} rewards...";
        }
        else
        {
            int discountPercent = GetDiscountPercent(fireParams.Multiplier);
            message += $"Applying {discountPercent}% discount to {rewardCount} rewards...";
        }

        CPH.SendMessage(message);
    }

    private void SendCompletionMessage(
        FireSaleParams fireParams,
        int rewardCount,
        double actualMultiplier
    )
    {
        string groupText = string.IsNullOrEmpty(fireParams.RewardGroups)
            ? "all rewards"
            : fireParams.RewardGroups;
        string message = "✅ Fire sale is now ACTIVE! ";

        if (fireParams.IsFree)
        {
            message += $"FREE (1 point) on {groupText} ({rewardCount} rewards)";
        }
        else if (fireParams.IsRandom)
        {
            int discountPercent = GetDiscountPercent(actualMultiplier);
            int minPercent = GetDiscountPercent(fireParams.RandomMax);
            int maxPercent = GetDiscountPercent(fireParams.RandomMin);
            message +=
                $"RANDOM {discountPercent}% discount applied on {groupText} ({rewardCount} rewards)";
        }
        else
        {
            int discountPercent = GetDiscountPercent(fireParams.Multiplier);
            message += $"{discountPercent}% discount on {groupText} ({rewardCount} rewards)";
        }

        if (!string.IsNullOrEmpty(fireParams.Duration))
        {
            int minutes = ParseDurationToMinutes(fireParams.Duration);
            message += $" for {minutes} minutes";
        }

        CPH.SendMessage(message);
    }
}
