using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#if EXTERNAL_EDITOR
public class DynamicTextCommands : CPHInlineBase
#else
public class CPHInline
#endif
{
    private static readonly Random _random = new Random();

    public enum TargetMode
    {
        Self,
        Random,
    }

    public class CommandConfig
    {
        public string Template { get; set; }
        public TargetMode TargetMode { get; set; }
        public Dictionary<string, object> Variables { get; set; } =
            new Dictionary<string, object>();
    }

    public class User
    {
        public string Id { get; set; }
        public string UserName { get; set; }
    }

    private static readonly Dictionary<string, CommandConfig> _commands = new Dictionary<
        string,
        CommandConfig
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["gay"] = new CommandConfig
        {
            Template = "@{user}, {target} {percent}% fruity (gay af) 🍇💅👑🌈",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object> { ["percent"] = new { min = 0, max = 100 } },
        },
        ["goodgirl"] = new CommandConfig
        {
            Template = "@{user}, {target} {percent}% goodgirl💅👑",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object> { ["percent"] = new { min = 0, max = 100 } },
        },
        ["goodboy"] = new CommandConfig
        {
            Template = "@{user}, {target} {percent}% goodboy 🍇💅",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object> { ["percent"] = new { min = 0, max = 100 } },
        },
        ["sweat"] = new CommandConfig
        {
            Template = "@{user}, {target} playing at a sweat level of {percent}% 🥵💦",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object> { ["percent"] = new { min = 0, max = 100 } },
        },
        ["stinky"] = new CommandConfig
        {
            Template = "@{user}, {target} {percent}% stinky today! 🦨💨 Time for a shower! 🚿",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object> { ["percent"] = new { min = 0, max = 100 } },
        },
        ["pp"] = new CommandConfig
        {
            Template = "@{user}, {target_possessive} pickle size is {size1}.{size2} inches 🥒🍆😏",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object>
            {
                ["size1"] = new { min = 0, max = 10 },
                ["size2"] = new { min = 0, max = 9 },
            },
        },
        ["pro"] = new CommandConfig
        {
            Template = "@{user}, {target} {skill}! 🏆🎮🔥",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object>
            {
                ["skill"] = new[]
                {
                    "a Fortnite pro",
                    "a bot",
                    "cracked",
                    "washed",
                    "the next World Cup champ",
                    "a default skin at heart",
                    "absolutely goated",
                    "built different",
                    "lowkey carry",
                    "the clutch king",
                    "straight up cracked",
                    "underrated af",
                },
            },
        },
        ["cringe"] = new CommandConfig
        {
            Template = "@{user}, {target} just did something {action}! 😬😂📱",
            TargetMode = TargetMode.Self,
            Variables = new Dictionary<string, object>
            {
                ["action"] = new[]
                {
                    "cringe",
                    "epic",
                    "totally normal",
                    "worthy of a TikTok",
                    "that made the lobby laugh",
                    "that made the lobby cry",
                    "absolutely unhinged",
                    "main character energy",
                    "NPC behavior",
                    "straight up cursed",
                },
            },
        },
        ["hug"] = new CommandConfig
        {
            Template = "@{user} gives {target_direct} a {hug_type} hug! 🤗💕💞",
            TargetMode = TargetMode.Random,
            Variables = new Dictionary<string, object>
            {
                ["hug_type"] = new[]
                {
                    "big warm",
                    "gentle",
                    "tight",
                    "comforting",
                    "loving",
                    "friendly",
                    "awkward",
                    "surprise",
                },
            },
        },
        ["kiss"] = new CommandConfig
        {
            Template = "@{user} gives {target_direct} a {kiss_type} kiss! 🤗💕💞",
            TargetMode = TargetMode.Random,
            Variables = new Dictionary<string, object>
            {
                ["kiss_type"] = new[]
                {
                    "sweet",
                    "virtual",
                    "on-the-cheek",
                    "forehead",
                    "gentle",
                    "butterfly",
                    "air",
                    "dramatic",
                    "speedy",
                    "single-peck",
                    "surprise",
                    "bouncy",
                    "mwah",
                    "French",
                    "Eskimo",
                    "spidey upside-down",
                    "cheeky",
                    "shy",
                    "midnight",
                    "sleepy",
                    "glam",
                },
            },
        },
        ["spank"] = new CommandConfig
        {
            Template = "@{user} spanks {target_direct}! 👋🍑 {reaction} 😳",
            TargetMode = TargetMode.Random,
            Variables = new Dictionary<string, object>
            {
                ["reaction"] = new[]
                {
                    "That's what you get!",
                    "Ouch!",
                    "Deserved!",
                    "Kinky!",
                    "Harder daddy!",
                    "Stop it!",
                    "Again!",
                },
            },
        },
        ["simp"] = new CommandConfig
        {
            Template = "@{user} is simping for {target_direct} in chat! 💖😍🥺",
            TargetMode = TargetMode.Random,
        },
        ["ratio"] = new CommandConfig
        {
            Template =
                "@{user} just got ratio'd by {target_direct}! Better luck next time! #ratio 📉😂",
            TargetMode = TargetMode.Random,
        },
        ["shiton"] = new CommandConfig
        {
            Template = "@{user} shit on {target_direct} 😳💩💢🤢",
            TargetMode = TargetMode.Random,
        },
    };

    public bool Execute()
    {
        if (!TryGetCommandArgs(out var args))
            return true;
        var command = args.Command.TrimStart('!').ToLower();
        if (!_commands.TryGetValue(command, out var config))
        {
            CPH.SendMessage("Unknown command.");
            return true;
        }

        HandleCommand(args, config);
        return true;
    }

    private bool TryGetCommandArgs(out CommandArgs args)
    {
        args = new CommandArgs();
        string command = null,
            user = null,
            userId = null,
            rawInput = null;
        bool success =
            CPH.TryGetArg("command", out command)
            && CPH.TryGetArg("user", out user)
            && CPH.TryGetArg("userId", out userId)
            && CPH.TryGetArg("rawInput", out rawInput);
        if (success)
        {
            args.Command = command;
            args.User = user;
            args.UserId = userId;
            args.RawInput = rawInput;
        }

        return success;
    }

    private void HandleCommand(CommandArgs args, CommandConfig config)
    {
        var target = DetermineTarget(args, config);
        if (target == null)
            return;
        var templateVars = BuildTemplateVariables(args.User, target.Username, config.Variables);
        var message = ReplaceTemplatePlaceholders(config.Template, templateVars);
        CPH.SendMessage(message);
    }

    private TargetInfo DetermineTarget(CommandArgs args, CommandConfig config)
    {
        if (!string.IsNullOrEmpty(args.RawInput))
        {
            return GetSpecifiedTarget(args);
        }

        if (config.TargetMode == TargetMode.Self)
        {
            return new TargetInfo { Username = args.User, IsValid = true };
        }

        // Random target mode
        var randomUser = GetRandomViewer("excludeFromRandom", true);
        if (randomUser == null)
        {
            CPH.SendMessage($"@{args.User}, no other viewers found!");
            return null;
        }

        return new TargetInfo { Username = randomUser, IsValid = true };
    }

    private TargetInfo GetSpecifiedTarget(CommandArgs args)
    {
        var inputUser = args.RawInput.Replace("@", "").Trim();
        var validUser = GetValidTwitchUser(inputUser);
        if (validUser == null)
        {
            CPH.SendMessage($"@{args.User}, the user '{inputUser}' does not exist.");
            return null;
        }

        return new TargetInfo { Username = validUser, IsValid = true };
    }

    private Dictionary<string, string> BuildTemplateVariables(
        string user,
        string targetUser,
        Dictionary<string, object> configVariables
    )
    {
        var isSelf = string.Equals(targetUser, user, StringComparison.OrdinalIgnoreCase);
        var variables = new Dictionary<string, string>
        {
            ["user"] = user,
            ["target"] = isSelf ? "you are" : $"@{targetUser} is",
            ["target_direct"] = isSelf ? "themselves" : $"@{targetUser}",
            ["target_possessive"] = isSelf ? "your" : $"@{targetUser}'s",
        };
        foreach (var variable in configVariables)
        {
            variables[variable.Key] = GenerateVariable(variable.Value);
        }

        return variables;
    }

    private string ReplaceTemplatePlaceholders(
        string template,
        Dictionary<string, string> variables
    )
    {
        string result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
        }

        return result;
    }

    private string GenerateVariable(object variableConfig)
    {
        // Check for string array first
        if (variableConfig is string[] options)
        {
            return options[_random.Next(options.Length)];
        }

        // Accept List<string>
        if (variableConfig is List<string> listOptions)
        {
            return listOptions[_random.Next(listOptions.Count)];
        }

        // Accept List<object>
        if (variableConfig is List<object> objOptions)
        {
            return objOptions.Count > 0
                ? objOptions[_random.Next(objOptions.Count)].ToString()
                : "";
        }

        // Accept Newtonsoft JArray (just in case)
        if (variableConfig is Newtonsoft.Json.Linq.JArray jArr)
        {
            return jArr.Count > 0 ? jArr[_random.Next(jArr.Count)].ToString() : "";
        }

        if (HasMinMaxProperties(variableConfig))
        {
            return GenerateRandomNumber(variableConfig);
        }

        return variableConfig.ToString();
    }

    private static bool HasMinMaxProperties(object obj)
    {
        var type = obj.GetType();
        return type.GetProperty("min") != null && type.GetProperty("max") != null;
    }

    private string GenerateRandomNumber(object range)
    {
        var type = range.GetType();
        var minProperty = type.GetProperty("min");
        var maxProperty = type.GetProperty("max");
        if (minProperty != null && maxProperty != null)
        {
            int min = Convert.ToInt32(minProperty.GetValue(range, null));
            int max = Convert.ToInt32(maxProperty.GetValue(range, null));
            return _random.Next(min, max + 1).ToString();
        }

        return range.ToString();
    }

    private string GetValidTwitchUser(string login)
    {
        var userInfo = CPH.TwitchGetUserInfoByLogin(login);
        return userInfo?.UserName;
    }

    private string GetRandomViewer(string excludeGroup, bool removeInvoker)
    {
        var currentViewersJson = CPH.GetGlobalVar<string>("currentViewers", false);
        if (string.IsNullOrEmpty(currentViewersJson))
        {
            CPH.LogInfo("No currentViewers data available");
            return null;
        }

        List<User> currentViewers;
        try
        {
            currentViewers = JsonConvert.DeserializeObject<List<User>>(currentViewersJson);
            if (currentViewers == null)
            {
                CPH.LogInfo("Failed to deserialize currentViewers data");
                return null;
            }
        }
        catch (Exception ex)
        {
            CPH.LogInfo($"Error deserializing currentViewers: {ex.Message}");
            return null;
        }

        if (removeInvoker && CPH.TryGetArg("userId", out string userId))
        {
            currentViewers.RemoveAll(x => x.Id == userId);
        }

        if (!string.IsNullOrEmpty(excludeGroup))
        {
            ExcludeGroupUsers(currentViewers, excludeGroup);
        }

        if (currentViewers.Count == 0)
        {
            CPH.LogInfo("No viewers found");
            return null;
        }

        var randomIndex = _random.Next(0, currentViewers.Count);
        return currentViewers[randomIndex].UserName;
    }

    private void ExcludeGroupUsers(List<User> currentViewers, string excludeGroup)
    {
        var groupUsers = CPH.UsersInGroup(excludeGroup);
        if (groupUsers.Count > 0)
        {
            // Create a list of exclude IDs for faster lookup
            var excludeIds = new List<string>();
            foreach (var groupUser in groupUsers)
            {
                excludeIds.Add(groupUser.Id);
            }

            // Remove users that are in the exclude group
            for (int i = currentViewers.Count - 1; i >= 0; i--)
            {
                if (excludeIds.Contains(currentViewers[i].Id))
                {
                    currentViewers.RemoveAt(i);
                }
            }
        }
    }

    private class CommandArgs
    {
        public string Command { get; set; }
        public string User { get; set; }
        public string UserId { get; set; }
        public string RawInput { get; set; }
    }

    private class TargetInfo
    {
        public string Username { get; set; }
        public bool IsValid { get; set; }
    }
}

// User class moved outside for clarity
public class User
{
    public string Id { get; set; }
    public string UserName { get; set; }
}
