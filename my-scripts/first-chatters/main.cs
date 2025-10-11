using System;

public class CPHInline
{
    private const int MAX_POSITIONS = 3;
    private readonly int[] AWARDS = { 1000, 500, 250 };
    private readonly string[] POSITIONS = { "first", "second", "third" };

    public bool Execute()
    {
        int currentCounter = CPH.GetGlobalVar<int>("chatCounter", false);

        // If we already have 3 chatters, disable this action and return
        if (ShouldStopProcessing())
            return false;

        CPH.TryGetArg("user", out string user);
        if (string.IsNullOrEmpty(user))
            return false;

        user = user.Replace("@", "").Trim();

        // Check if user should be excluded
        if (ShouldExcludeUser(user))
            return false;

        // Award position and points
        AwardPosition(user, currentCounter);

        // Increment counter
        CPH.SetGlobalVar("chatCounter", currentCounter + 1, false);

        // If this was the 3rd chatter, disable the action
        ShouldStopProcessing();

        return true;
    }

    private bool ShouldExcludeUser(string user)
    {
        var broadcaster = CPH.TwitchGetBroadcaster();
        var bot = CPH.TwitchGetBot();

        string broadcasterName = broadcaster?.UserName;
        string botName = bot?.UserName;

        // Check against broadcaster, bot, and existing positions
        if (
            string.Equals(user, broadcasterName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user, botName, StringComparison.OrdinalIgnoreCase)
        )
            return true;

        // Check against existing positions
        for (int i = 0; i < MAX_POSITIONS; i++)
        {
            string existingUser = CPH.GetGlobalVar<string>(POSITIONS[i], false);
            if (string.Equals(user, existingUser, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void AwardPosition(string user, int position)
    {
        // Set the position (non-persistent since it's session-based)
        CPH.SetGlobalVar(POSITIONS[position], user, false);

        // Get user info for Twitch user vars
        var userInfo = CPH.TwitchGetUserInfoByLogin(user);
        if (userInfo == null)
            return;

        // Update user's top 3 counter using Twitch user var (persistent)
        string topThreeCounter = CPH.GetTwitchUserVarById<string>(
            userInfo.UserId,
            "topThreeCount",
            true
        );
        int currentCount = 0;
        if (
            !string.IsNullOrEmpty(topThreeCounter)
            && int.TryParse(topThreeCounter, out currentCount)
        )
        {
            currentCount++;
        }
        else
        {
            currentCount = 1;
        }
        CPH.SetTwitchUserVarById(userInfo.UserId, "topThreeCount", currentCount, true);

        // Award points (persistent)
        AwardPoints(userInfo.UserId, AWARDS[position]);

        // Send congratulatory message
        CPH.SendMessage(
            $"Congrats @{user}, you're {POSITIONS[position]} and have made the top three {currentCount} times! (+{AWARDS[position]} points)"
        );
    }

    private void AwardPoints(string userId, int points)
    {
        string pointsVarName = "points";
        string currentPointsStr = CPH.GetTwitchUserVarById<string>(userId, pointsVarName, true);

        long currentPoints = 0;
        if (
            !string.IsNullOrEmpty(currentPointsStr)
            && long.TryParse(currentPointsStr, out currentPoints)
        )
        {
            currentPoints += points;
        }
        else
        {
            currentPoints = points;
        }

        CPH.SetTwitchUserVarById(userId, pointsVarName, currentPoints, true);
    }

    private bool ShouldStopProcessing()
    {
        int currentCounter = CPH.GetGlobalVar<int>("chatCounter", false);
        if (currentCounter >= MAX_POSITIONS)
        {
            CPH.DisableActionById("de46f6ec-0a00-4760-a9fe-ae2c671f3f6f");
            return true;
        }
        return false;
    }
}
