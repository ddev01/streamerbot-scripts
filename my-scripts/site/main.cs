using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

public class CPHInline
{
    // You can set the timeout to any length you need
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    // Custom log file path
    private const string LOG_FILE_PATH = "logs/streamerbot_poster.log";

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
            // Test individual functions by uncommenting one of these:

            // Test users.dat data import
            return PostUsersData();

            // Test globals.db stats import
            // return PostStatsData();

            // Original test data
            // return PostTestData();
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
    /// Posts users.dat data to Laravel /api/twitch/users endpoint
    /// </summary>
    public bool PostUsersData()
    {
        try
        {
            // Parse the raw users.dat JSON data
            var usersData = ParseUsersDatFile();

            if (usersData == null || usersData.Count == 0)
            {
                WriteLog("WARN", "No users data found to send");
                return false;
            }

            var payload = new { users = usersData };

            string json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = _httpClient
                .PostAsync(
                    "https://gargoyled-gearldine-interfamily.ngrok-free.dev/api/twitch/users",
                    content
                )
                .GetAwaiter()
                .GetResult();

            if (response.IsSuccessStatusCode)
            {
                WriteLog(
                    "INFO",
                    $"Users data sent successfully! ({usersData.Count} users) - Status: {response.StatusCode}"
                );
                return true;
            }
            else
            {
                WriteLog(
                    "WARN",
                    $"Users POST failed: {response.StatusCode} - {response.ReasonPhrase}"
                );
                return false;
            }
        }
        catch (Exception ex)
        {
            WriteLog(
                "ERROR",
                $"Users POST error: {ex.Message} | Inner: {ex.InnerException?.Message ?? "None"} | StackTrace: {ex.StackTrace}"
            );
            return false;
        }
    }

    /// <summary>
    /// Parses the users.dat JSON file and converts it to the format expected by Laravel
    /// </summary>
    private List<object> ParseUsersDatFile()
    {
        try
        {
            // Raw string literal - no quote escaping needed!
            var usersDatJson = """
{
  "version": 9,
  "t": "2025-10-24T03:26:26.22007+02:00",
  "groups": [
    {
      "id": "65a4e40c-2646-4951-8d87-6e2bc61b3199",
      "name": "excludeFromRandom",
      "bots": false,
      "userIds": [
        "twitch_112699727",
        "twitch_100135110",
        "twitch_646848961"
      ]
    }
  ],
  "users": {
    "twitch_1025662819": {
      "id": "1025662819",
      "name": "ahalpha89",
      "display": "ahalpha89",
      "role": 0,
      "subscribed": false,
      "type": "twitch",
      "present": false,
      "previousActive": "2025-10-24T03:11:21.3426029+02:00",
      "lastActive": "2025-07-16T13:31:22.3471389+02:00",
      "exempt": false
    },
    "twitch_112699727": {
      "id": "112699727",
      "name": "mychoppaeats",
      "display": "mychoppaeats",
      "role": 4,
      "subscribed": true,
      "type": "twitch",
      "present": true,
      "previousActive": "2025-10-24T03:16:31.369179+02:00",
      "lastActive": "2025-10-24T03:21:31.3785929+02:00",
      "exempt": false
    },
    "twitch_408614165": {
      "id": "408614165",
      "name": "ayoplutox",
      "display": "ayoplutox",
      "role": 3,
      "subscribed": true,
      "type": "twitch",
      "present": false,
      "previousActive": "2025-10-24T03:11:21.3446029+02:00",
      "lastActive": "2025-10-22T22:34:02.4404812+02:00",
      "exempt": false
    },
    "twitch_1101744664": {
      "id": "1101744664",
      "name": "nokillskiera",
      "display": "nokillskiera",
      "role": 3,
      "subscribed": true,
      "type": "twitch",
      "present": false,
      "previousActive": "2025-10-24T03:11:21.3446029+02:00",
      "lastActive": "2025-10-22T22:39:02.4635683+02:00",
      "exempt": false
    },
    "twitch_1324186781": {
      "id": "1324186781",
      "name": "127aac74ac",
      "display": "127aac74ac",
      "role": 0,
      "subscribed": false,
      "type": "twitch",
      "present": false,
      "previousActive": "2025-10-24T03:11:21.3426029+02:00",
      "lastActive": "2025-07-25T05:15:07.5877607+02:00",
      "exempt": false
    }
  }
}
""";

            // Parse the JSON using strongly typed classes
            var usersDat = JsonConvert.DeserializeObject<UsersDatRoot>(usersDatJson);
            var usersList = new List<object>();

            // Extract users from the "users" dictionary
            foreach (var user in usersDat.users.Values)
            {
                usersList.Add(
                    new
                    {
                        id = user.id,
                        name = user.name,
                        display = user.display,
                        role = user.role,
                        subscribed = user.subscribed,
                        type = user.type,
                        present = user.present,
                        lastActive = user.lastActive,
                    }
                );
            }

            return usersList;
        }
        catch (Exception ex)
        {
            WriteLog(
                "ERROR",
                $"Error parsing users.dat: {ex.Message} | Inner: {ex.InnerException?.Message ?? "None"} | StackTrace: {ex.StackTrace}"
            );
            return new List<object>();
        }
    }

    /// <summary>
    /// Posts globals.db stats data to Laravel /api/twitch/stats endpoint
    /// </summary>
    public bool PostStatsData()
    {
        try
        {
            // Parse the raw globals.db JSON data
            var statsData = ParseGlobalsDbFile();

            if (statsData == null || statsData.Count == 0)
            {
                WriteLog("WARN", "No stats data found to send");
                return false;
            }

            var payload = new { stats = statsData };

            string json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = _httpClient
                .PostAsync(
                    "https://gargoyled-gearldine-interfamily.ngrok-free.dev/api/twitch/stats",
                    content
                )
                .GetAwaiter()
                .GetResult();

            if (response.IsSuccessStatusCode)
            {
                WriteLog(
                    "INFO",
                    $"Stats data sent successfully! ({statsData.Count} stats) - Status: {response.StatusCode}"
                );
                return true;
            }
            else
            {
                WriteLog(
                    "WARN",
                    $"Stats POST failed: {response.StatusCode} - {response.ReasonPhrase}"
                );
                return false;
            }
        }
        catch (Exception ex)
        {
            WriteLog(
                "ERROR",
                $"Stats POST error: {ex.Message} | Inner: {ex.InnerException?.Message ?? "None"} | StackTrace: {ex.StackTrace}"
            );
            return false;
        }
    }

    /// <summary>
    /// Parses the globals.db JSON file and converts it to the format expected by Laravel
    /// </summary>
    private List<object> ParseGlobalsDbFile()
    {
        try
        {
            // Raw string literal - no quote escaping needed!
            var globalsDbJson = """
[
  {
    "_id": {"$oid": "6876a2a3c937df00deb54b04"},
    "userId": "112699727",
    "platform": "twitch",
    "name": "hotPotatoesPassed",
    "value": 14,
    "lastWrite": {"$date": "2025-07-31T17:47:25.1000000Z"}
  },
  {
    "_id": {"$oid": "6879650dc937df0c9a99eaec"},
    "userId": "112699727",
    "platform": "twitch",
    "name": "points",
    "value": {"$numberLong": "3000"},
    "lastWrite": {"$date": "2025-10-21T18:30:44.8350000Z"}
  },
  {
    "_id": {"$oid": "687ac047c937df027d504882"},
    "userId": "112699727",
    "platform": "twitch",
    "name": "lurkCount",
    "value": 1,
    "lastWrite": {"$date": "2025-07-18T21:44:39.0260000Z"}
  },
  {
    "_id": {"$oid": "687ac04dc937df027d504886"},
    "userId": "112699727",
    "platform": "twitch",
    "name": "lurkTime",
    "value": "\"00:00:06.1588857\"",
    "lastWrite": {"$date": "2025-07-18T21:44:45.1900000Z"}
  },
  {
    "_id": {"$oid": "687ad7b9c937df09ee1749a3"},
    "userId": "112699727",
    "platform": "twitch",
    "name": "watchtime",
    "value": {"$numberLong": "1314780"},
    "lastWrite": {"$date": "2025-07-18T23:49:25.5890000Z"}
  },
  {
    "_id": {"$oid": "68ebb6f5c937df124ef95cbb"},
    "userId": "112699727",
    "platform": "twitch",
    "name": "topThreeCount",
    "value": 30,
    "lastWrite": {"$date": "2025-10-12T14:51:09.5200000Z"}
  },
  {
    "_id": {"$oid": "6876c4b9c937df00deb54b75"},
    "userId": "408614165",
    "platform": "twitch",
    "name": "hotPotatoesCaught",
    "value": 46,
    "lastWrite": {"$date": "2025-07-28T19:49:10.0490000Z"}
  },
  {
    "_id": {"$oid": "6876c4c1c937df00deb54b79"},
    "userId": "408614165",
    "platform": "twitch",
    "name": "hotPotatoesPassed",
    "value": 60,
    "lastWrite": {"$date": "2025-09-13T13:37:20.1080000Z"}
  },
  {
    "_id": {"$oid": "6877fd93c937df1241a3c277"},
    "userId": "408614165",
    "platform": "twitch",
    "name": "points",
    "value": {"$numberLong": "367240"},
    "lastWrite": {"$date": "2025-10-22T20:34:02.7970000Z"}
  },
  {
    "_id": {"$oid": "6877fea1c937df1241a3c285"},
    "userId": "408614165",
    "platform": "twitch",
    "name": "watchtime",
    "value": {"$numberLong": "1904160"},
    "lastWrite": {"$date": "2025-10-22T20:34:02.7820000Z"}
  },
  {
    "_id": {"$oid": "689f8735c937df0614dbb1e1"},
    "userId": "408614165",
    "platform": "twitch",
    "name": "lurkCount",
    "value": 2,
    "lastWrite": {"$date": "2025-08-28T20:38:42.0660000Z"}
  },
  {
    "_id": {"$oid": "689f8addc937df0614dbb1f8"},
    "userId": "408614165",
    "platform": "twitch",
    "name": "lurkTime",
    "value": "\"00:18:59.4500786\"",
    "lastWrite": {"$date": "2025-08-28T20:42:06.1060000Z"}
  },
  {
    "_id": {"$oid": "68ebe236c937df0be0d3f293"},
    "userId": "408614165",
    "platform": "twitch",
    "name": "topThreeCount",
    "value": 28,
    "lastWrite": {"$date": "2025-10-20T20:58:26.6280000Z"}
  },
  {
    "_id": {"$oid": "6876ab5fc937df00deb54b3c"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "hotPotatoesCaught",
    "value": 56,
    "lastWrite": {"$date": "2025-07-31T17:47:37.2910000Z"}
  },
  {
    "_id": {"$oid": "6876ab66c937df00deb54b40"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "hotPotatoesPassed",
    "value": 55,
    "lastWrite": {"$date": "2025-07-31T17:47:41.6160000Z"}
  },
  {
    "_id": {"$oid": "6877fd89c937df1241a3c275"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "points",
    "value": {"$numberLong": "99891"},
    "lastWrite": {"$date": "2025-10-22T20:34:02.8240000Z"}
  },
  {
    "_id": {"$oid": "6877fea1c937df1241a3c288"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "watchtime",
    "value": {"$numberLong": "2127900"},
    "lastWrite": {"$date": "2025-10-22T20:34:02.8140000Z"}
  },
  {
    "_id": {"$oid": "687bdf7dc937df033a274f78"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "hotPotatoBurned",
    "value": 2,
    "lastWrite": {"$date": "2025-07-19T23:12:54.8460000Z"}
  },
  {
    "_id": {"$oid": "687d5c45c937df0ed95305e6"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "lurkCount",
    "value": 2,
    "lastWrite": {"$date": "2025-08-19T19:31:24.6500000Z"}
  },
  {
    "_id": {"$oid": "687d6eccc937df0ed953060d"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "lurkTime",
    "value": "\"01:59:32.4973574\"",
    "lastWrite": {"$date": "2025-08-19T20:11:53.3170000Z"}
  },
  {
    "_id": {"$oid": "68dd8318c937df03fe2e1742"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "tawmae_twitch_points_followingFlag",
    "value": true,
    "lastWrite": {"$date": "2025-10-01T19:38:00.7090000Z"}
  },
  {
    "_id": {"$oid": "68ebe263c937df0be0d3f295"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "topThreeCount",
    "value": 26,
    "lastWrite": {"$date": "2025-10-22T18:02:20.3200000Z"}
  },
  {
    "_id": {"$oid": "68ed8bbfc937df08c475e154"},
    "userId": "1101744664",
    "platform": "twitch",
    "name": "twitchRaffle_UserEntries",
    "value": 1,
    "lastWrite": {"$date": "2025-10-13T23:31:11.1250000Z"}
  },
  {
    "_id": {"$oid": "687806aac937df1241a3c360"},
    "userId": "438270497",
    "platform": "twitch",
    "name": "points",
    "value": {"$numberLong": "7225"},
    "lastWrite": {"$date": "2025-10-22T18:14:02.5150000Z"}
  },
  {
    "_id": {"$oid": "687806c0c937df1241a3c369"},
    "userId": "438270497",
    "platform": "twitch",
    "name": "hotPotatoesCaught",
    "value": 1,
    "lastWrite": {"$date": "2025-07-16T20:08:32.8120000Z"}
  },
  {
    "_id": {"$oid": "687806d0c937df1241a3c36a"},
    "userId": "438270497",
    "platform": "twitch",
    "name": "hotPotatoBurned",
    "value": 1,
    "lastWrite": {"$date": "2025-07-16T20:08:48.1660000Z"}
  },
  {
    "_id": {"$oid": "68780801c937df1241a3c3b9"},
    "userId": "438270497",
    "platform": "twitch",
    "name": "watchtime",
    "value": {"$numberLong": "1255800"},
    "lastWrite": {"$date": "2025-10-22T18:14:02.5010000Z"}
  },
  {
    "_id": {"$oid": "68819f33c937df10d658a25d"},
    "userId": "438270497",
    "platform": "twitch",
    "name": "lurkCount",
    "value": 1,
    "lastWrite": {"$date": "2025-07-24T02:49:23.5140000Z"}
  }
]
""";

            // Parse the JSON array using strongly typed classes
            var statsArray = JsonConvert.DeserializeObject<StatInfo[]>(globalsDbJson);
            var statsList = new List<object>();

            // Convert each stat object to the format expected by Laravel
            foreach (var stat in statsArray)
            {
                // Handle different value types (numbers, strings, booleans)
                object value = stat.value;

                // If value is a complex object (like $numberLong), extract the actual value
                if (stat.value is Newtonsoft.Json.Linq.JObject valueObj)
                {
                    if (valueObj.ContainsKey("$numberLong"))
                    {
                        value = long.Parse((string)valueObj["$numberLong"]);
                    }
                    else if (valueObj.ContainsKey("$date"))
                    {
                        value = (string)valueObj["$date"];
                    }
                }

                statsList.Add(
                    new
                    {
                        userId = stat.userId,
                        platform = stat.platform,
                        name = stat.name,
                        value = value,
                        lastWrite = stat.lastWrite.date,
                    }
                );
            }

            return statsList;
        }
        catch (Exception ex)
        {
            WriteLog(
                "ERROR",
                $"Error parsing globals.db: {ex.Message} | Inner: {ex.InnerException?.Message ?? "None"} | StackTrace: {ex.StackTrace}"
            );
            return new List<object>();
        }
    }

    /// <summary>
    /// Original test data function
    /// </summary>
    public bool PostTestData()
    {
        try
        {
            // Build the payload object
            var data = new { test = "pcdata", from = "csharp-app" };
            // Serialize to JSON
            string json = JsonConvert.SerializeObject(data);
            // Create the request payload
            var payload = new StringContent(json, Encoding.UTF8, "application/json");
            // Send the POST request
            HttpResponseMessage response = _httpClient
                .PostAsync(
                    "https://gargoyled-gearldine-interfamily.ngrok-free.dev/api/receive-data",
                    payload
                )
                .GetAwaiter()
                .GetResult();
            // Check if the request was successful
            if (response.IsSuccessStatusCode)
            {
                WriteLog("INFO", $"HTTP POST successful: {response.StatusCode}");
            }
            else
            {
                WriteLog(
                    "WARN",
                    $"HTTP POST failed: {response.StatusCode} - {response.ReasonPhrase}"
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            WriteLog(
                "ERROR",
                $"HTTP POST error: {ex.Message} | Inner: {ex.InnerException?.Message ?? "None"} | StackTrace: {ex.StackTrace}"
            );
            return false;
        }
    }
}

// Strongly typed classes to replace dynamic usage
public class UsersDatRoot
{
    public int version { get; set; }
    public string t { get; set; }
    public List<Group> groups { get; set; }
    public Dictionary<string, UserInfo> users { get; set; }
}

public class Group
{
    public string id { get; set; }
    public string name { get; set; }
    public bool bots { get; set; }
    public List<string> userIds { get; set; }
}

public class UserInfo
{
    public string id { get; set; }
    public string name { get; set; }
    public string display { get; set; }
    public int role { get; set; }
    public bool subscribed { get; set; }
    public string type { get; set; }
    public bool present { get; set; }
    public string previousActive { get; set; }
    public string lastActive { get; set; }
    public bool exempt { get; set; }
}

public class StatInfo
{
    public ObjectId _id { get; set; }
    public string userId { get; set; }
    public string platform { get; set; }
    public string name { get; set; }
    public object value { get; set; }
    public LastWrite lastWrite { get; set; }
}

public class ObjectId
{
    [JsonProperty("$oid")]
    public string oid { get; set; }
}

public class LastWrite
{
    [JsonProperty("$date")]
    public string date { get; set; }
}
