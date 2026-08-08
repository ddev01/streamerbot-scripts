using System;
using System.IO;

#if EXTERNAL_EDITOR
public class AutoPurgeMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    // Configuration
    private const string BOT_USERNAME = "YourBotName"; // Change this to your bot's username
    private const int MESSAGE_TIMEOUT_SECONDS = 30;
    private const string LOG_FILE_PATH = "logs/auto-purge.log";

    /// Called by timer every 30 seconds to check and purge old bot messages
    public bool Execute()
    {
        try
        {
            // Get bot info
            var botInfo = CPH.TwitchGetBot();
            if (botInfo == null)
            {
                WriteLog("WARN", "Bot user not found");
                return false;
            }

            string botUserId = botInfo.UserId;
            string botUsername = botInfo.UserName;

            // Get the bot's last message timestamp using the same user var as hot potato
            var lastMessageTime = CPH.GetTwitchUserVarById<DateTime>(
                botUserId,
                "hotPotato_isActive",
                false
            );

            // If no timestamp exists, nothing to purge
            if (lastMessageTime == default(DateTime))
            {
                WriteLog("DEBUG", "No bot message timestamp found");
                return true;
            }

            // Calculate time since last message
            TimeSpan timeSinceLastMessage = DateTime.Now - lastMessageTime;

            WriteLog(
                "DEBUG",
                $"Last bot message was {timeSinceLastMessage.TotalSeconds:F1} seconds ago"
            );

            // If last message is older than threshold, purge bot messages
            if (timeSinceLastMessage.TotalSeconds >= MESSAGE_TIMEOUT_SECONDS)
            {
                WriteLog(
                    "INFO",
                    $"Purging bot messages (last message was {timeSinceLastMessage.TotalSeconds:F1}s ago)"
                );

                // Timeout for 1 second to clear messages
                CPH.TwitchTimeoutUser(botUsername, 1, "Auto-purge old messages");
                CPH.Wait(1100); // Wait slightly longer than timeout duration

                WriteLog("INFO", "Bot messages purged successfully");

                // Clear the timestamp after purging
                CPH.UnsetTwitchUserVarById(botUserId, "hotPotato_isActive", false);
            }
            else
            {
                WriteLog(
                    "DEBUG",
                    $"Bot message still fresh ({timeSinceLastMessage.TotalSeconds:F1}s < {MESSAGE_TIMEOUT_SECONDS}s)"
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            WriteLog("ERROR", $"Auto-purge error: {ex.Message}");
            return false;
        }
    }

    /// Writes log messages to a dedicated auto-purge log file
    private void WriteLog(string level, string message)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] [{level}] {message}";

            // Ensure the logs directory exists
            string logDirectory = Path.GetDirectoryName(LOG_FILE_PATH);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Append to log file
            File.AppendAllText(LOG_FILE_PATH, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Fallback to CPH logging if file writing fails
            CPH.LogError($"Failed to write to auto-purge log: {ex.Message}");
        }
    }
}
