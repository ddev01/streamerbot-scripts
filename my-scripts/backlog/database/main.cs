using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

#if EXTERNAL_EDITOR
public class DatabaseMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    // ===== CONFIGURATION =====
    private const string DATABASE_DIR = "mydatabase";

    // ===== DATA MODELS =====
    public class DatabaseEntry
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }

    public class DatabaseCount
    {
        public string Username { get; set; }
        public int Count { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public bool Execute()
    {
        return true;
    }

    // ===== PUBLIC FUNCTIONS (Called by Streamer.bot actions) =====

    /// <summary>
    /// Records when someone raids us - called by "Raided From" trigger
    /// </summary>
    public bool StoreRaidFromInDatabase()
    {
        try
        {
            // 1. Get and validate args
            if (!CPH.TryGetArg("username", out string username) || string.IsNullOrEmpty(username))
            {
                Log("ERROR", "StoreRaidFromInDatabase: No username provided");
                return false;
            }

            CPH.TryGetArg("viewers", out int viewers);
            CPH.TryGetArg("userId", out string userId);

            // 2. Prepare data payload
            var entryData = new Dictionary<string, object>
            {
                { "viewers", viewers },
                { "userId", userId ?? "unknown" },
            };

            // 3. Use modular database functions
            string dbName = "raids";
            EnsureDatabaseExists(dbName);

            // Insert into history table
            bool historySuccess = InsertHistoryEntry(
                dbName,
                "raid_from_history",
                username,
                entryData
            );

            // Update count table
            bool countSuccess = IncrementUserCount(dbName, "raid_from_counts", username);

            if (historySuccess && countSuccess)
            {
                Log("SUCCESS", $"Raid from {username} ({viewers} viewers) stored successfully");
            }

            return historySuccess && countSuccess;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"StoreRaidFromInDatabase error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Records when we raid someone else - called by "Raided To" trigger
    /// </summary>
    public bool StoreRaidToInDatabase()
    {
        try
        {
            // 1. Get and validate args
            if (!CPH.TryGetArg("targetUser", out string username) || string.IsNullOrEmpty(username))
            {
                Log("ERROR", "StoreRaidToInDatabase: No target username provided");
                return false;
            }

            CPH.TryGetArg("viewers", out int viewers);
            CPH.TryGetArg("targetUserId", out string userId);

            // 2. Prepare data payload
            var entryData = new Dictionary<string, object>
            {
                { "viewers", viewers },
                { "userId", userId ?? "unknown" },
            };

            // 3. Use modular database functions
            string dbName = "raids";
            EnsureDatabaseExists(dbName);

            // Insert into history table
            bool historySuccess = InsertHistoryEntry(
                dbName,
                "raid_to_history",
                username,
                entryData
            );

            // Update count table
            bool countSuccess = IncrementUserCount(dbName, "raid_to_counts", username);

            if (historySuccess && countSuccess)
            {
                Log("SUCCESS", $"Raid to {username} ({viewers} viewers) stored successfully");
            }

            return historySuccess && countSuccess;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"StoreRaidToInDatabase error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Example: Store chat messages - called by "Chat Message" trigger
    /// </summary>
    public bool StoreChatMessageInDatabase()
    {
        try
        {
            // 1. Get and validate args
            if (!CPH.TryGetArg("user", out string username) || string.IsNullOrEmpty(username))
            {
                Log("ERROR", "StoreChatMessageInDatabase: No username provided");
                return false;
            }

            if (!CPH.TryGetArg("message", out string message) || string.IsNullOrEmpty(message))
            {
                Log("ERROR", "StoreChatMessageInDatabase: No message provided");
                return false;
            }

            CPH.TryGetArg("userId", out string userId);
            CPH.TryGetArg("isSubscriber", out bool isSubscriber);
            CPH.TryGetArg("isModerator", out bool isModerator);
            CPH.TryGetArg("isVip", out bool isVip);
            CPH.TryGetArg("msgId", out string msgId);
            CPH.TryGetArg("bits", out int bits);

            // 2. Prepare data payload
            var entryData = new Dictionary<string, object>
            {
                { "message", message },
                { "userId", userId ?? "unknown" },
                { "isSubscriber", isSubscriber },
                { "isModerator", isModerator },
                { "isVip", isVip },
                { "msgId", msgId ?? "" },
                { "bits", bits },
            };

            // 3. Use modular database functions
            string dbName = "twitch";
            EnsureDatabaseExists(dbName);

            // Insert into messages table
            bool historySuccess = InsertHistoryEntry(dbName, "messages", username, entryData);

            // Update count table (total messages per user)
            bool countSuccess = IncrementUserCount(dbName, "message_counts", username);

            return historySuccess && countSuccess;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"StoreChatMessageInDatabase error: {ex.Message}");
            return false;
        }
    }

    // ===== MODULAR DATABASE HELPER FUNCTIONS =====

    /// <summary>
    /// Ensures database directory structure exists
    /// </summary>
    private void EnsureDatabaseExists(string databaseName)
    {
        string dbPath = Path.Combine(DATABASE_DIR, databaseName);
        if (!Directory.Exists(dbPath))
        {
            Directory.CreateDirectory(dbPath);
            Log("INFO", $"Created database directory: {dbPath}");
        }
    }

    /// <summary>
    /// Inserts a history entry into a table
    /// </summary>
    private bool InsertHistoryEntry(
        string databaseName,
        string tableName,
        string username,
        Dictionary<string, object> additionalData
    )
    {
        try
        {
            string tablePath = GetTablePath(databaseName, tableName);

            // Create entry
            var entry = new DatabaseEntry
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                Timestamp = DateTime.Now,
                Data = additionalData ?? new Dictionary<string, object>(),
            };

            // Read existing entries
            List<DatabaseEntry> entries = ReadTableData<DatabaseEntry>(tablePath);

            // Add new entry
            entries.Add(entry);

            // Write back
            WriteTableData(tablePath, entries);

            Log("DEBUG", $"Inserted history entry for {username} into {tableName}");
            return true;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"InsertHistoryEntry error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Increments a user's count in a count table
    /// </summary>
    private bool IncrementUserCount(string databaseName, string tableName, string username)
    {
        try
        {
            string tablePath = GetTablePath(databaseName, tableName);

            // Read existing counts
            List<DatabaseCount> counts = ReadTableData<DatabaseCount>(tablePath);

            // Find or create count entry
            var countEntry = counts.FirstOrDefault(c =>
                c.Username.Equals(username, StringComparison.OrdinalIgnoreCase)
            );

            if (countEntry == null)
            {
                countEntry = new DatabaseCount
                {
                    Username = username,
                    Count = 1,
                    LastUpdated = DateTime.Now,
                };
                counts.Add(countEntry);
            }
            else
            {
                countEntry.Count++;
                countEntry.LastUpdated = DateTime.Now;
            }

            // Write back
            WriteTableData(tablePath, counts);

            Log("DEBUG", $"Updated count for {username} in {tableName}: {countEntry.Count}");
            return true;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"IncrementUserCount error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generic function to read table data
    /// </summary>
    private List<T> ReadTableData<T>(string tablePath)
    {
        try
        {
            if (!File.Exists(tablePath))
            {
                return new List<T>();
            }

            string json = File.ReadAllText(tablePath);
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }
        catch (Exception ex)
        {
            Log("ERROR", $"ReadTableData error: {ex.Message}");
            return new List<T>();
        }
    }

    /// <summary>
    /// Generic function to write table data
    /// </summary>
    private void WriteTableData<T>(string tablePath, List<T> data)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(tablePath, json);
    }

    /// <summary>
    /// Gets the full path to a table file
    /// </summary>
    private string GetTablePath(string databaseName, string tableName)
    {
        return Path.Combine(DATABASE_DIR, databaseName, $"{tableName}.json");
    }

    /// <summary>
    /// Logging helper
    /// </summary>
    private void Log(string level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        CPH.LogInfo($"[{timestamp}] [{level}] [custom-database] {message}");
    }
}
