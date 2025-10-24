using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

public class CPHInline
{
    // ===== CONFIGURATION =====
    private const string API_BASE_URL = "https://gargoyled-gearldine-interfamily.ngrok-free.dev";
    private string API_TOKEN;
    private const string LOG_FILE_PATH = "logs/streamerbot_poster.log";
    private const int TEST_LIMIT = 999; // Set to 999 to process all stats, or 1 for testing

    // List of stat variables to sync
    private static readonly List<string> STAT_NAMES = new List<string>
    {
        "hotPotatoesCaught",
        "hotPotatoesPassed",
        "points",
        "watchtime",
        "lurkCount",
        "lurkTime",
        "topThreeCount",
    };

    // ===== HTTP CLIENT =====
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// Custom log function that writes to a specific log file
    /// </summary>
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

    public void Init()
    {
        // Ensure we are working with a clean slate
        _httpClient.DefaultRequestHeaders.Clear();

        // Get API token from global var
        //API_TOKEN = CPH.GetGlobalVar<string>("laravelApiKey", true);
        API_TOKEN = "dev_9LnUBPRwcXiXOrQmEhFzYzgIjKMY8lrHkV2yNZeM";

        // Add API token for authentication
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_TOKEN}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "StreamerBot-Poster/1.0");
    }

    public void Dispose()
    {
        // Free up allocations
        _httpClient.Dispose();
    }

    public bool Execute()
    {
        try
        {
            return PostAllUserStats();
        }
        catch (Exception ex)
        {
            WriteLog(
                "ERROR",
                $"Execute error: {ex.Message} | Inner: {ex.InnerException?.Message ?? "None"} | StackTrace: {ex.StackTrace}"
            );
            return false;
        }
    }

    /// <summary>
    /// Posts all user stats using CPH API
    /// </summary>
    private bool PostAllUserStats()
    {
        try
        {
            string mode = TEST_LIMIT >= STAT_NAMES.Count ? "FULL" : "TEST";
            WriteLog(
                "INFO",
                $"Starting stats sync ({mode} MODE: {TEST_LIMIT}/{STAT_NAMES.Count} stats)"
            );

            int processedCount = 0;

            foreach (var statName in STAT_NAMES)
            {
                if (processedCount >= TEST_LIMIT)
                {
                    WriteLog("INFO", $"Limit reached ({TEST_LIMIT}), stopping");
                    break;
                }

                if (!ProcessStat(statName))
                {
                    return false;
                }

                processedCount++;
            }

            WriteLog(
                "INFO",
                $"✓ Stats sync completed! Processed {processedCount}/{STAT_NAMES.Count} stat(s)"
            );
            return true;
        }
        catch (Exception ex)
        {
            WriteLog(
                "ERROR",
                $"PostAllUserStats error: {ex.Message} | Inner: {ex.InnerException?.Message ?? "None"}"
            );
            return false;
        }
    }

    /// <summary>
    /// Process and post a single stat type
    /// </summary>
    private bool ProcessStat(string statName)
    {
        try
        {
            WriteLog("INFO", $"Processing: {statName}");

            // Get all users with this variable from CPH
            var userVarList = CPH.GetTwitchUsersVar<object>(statName, true);

            if (userVarList == null || userVarList.Count == 0)
            {
                WriteLog("WARN", $"No data for: {statName}");
                return true; // Not an error, just skip
            }

            WriteLog("INFO", $"Found {userVarList.Count} users with {statName}");

            // Transform to Laravel API format
            var statsPayload = new List<object>();
            foreach (var userVar in userVarList)
            {
                statsPayload.Add(
                    new
                    {
                        userId = userVar.UserId,
                        userName = userVar.UserName,
                        platform = userVar.UserType,
                        name = statName,
                        value = userVar.Value,
                        lastWrite = userVar.LastWrite,
                    }
                );
            }

            // Post to Laravel API
            return PostToApi(statsPayload, statName);
        }
        catch (Exception ex)
        {
            WriteLog("ERROR", $"ProcessStat({statName}) error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Post stats payload to Laravel API
    /// </summary>
    private bool PostToApi(List<object> statsPayload, string statName)
    {
        try
        {
            var payload = new { stats = statsPayload };
            string json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string url = $"{API_BASE_URL}/api/twitch/stats";
            WriteLog("INFO", $"Posting {statsPayload.Count} {statName} records");

            HttpResponseMessage response = _httpClient
                .PostAsync(url, content)
                .GetAwaiter()
                .GetResult();

            if (response.IsSuccessStatusCode)
            {
                WriteLog("INFO", $"✓ Posted {statName} - {response.StatusCode}");
                return true;
            }
            else
            {
                WriteLog(
                    "ERROR",
                    $"✗ Failed {statName} - {response.StatusCode}: {response.ReasonPhrase}"
                );
                return false;
            }
        }
        catch (Exception ex)
        {
            WriteLog("ERROR", $"PostToApi({statName}) error: {ex.Message}");
            return false;
        }
    }
}
