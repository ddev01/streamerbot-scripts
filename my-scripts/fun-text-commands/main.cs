using FluentConfig;
using FluentConfig.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Refs: System, System.Core, FluentConfig.dll (Streamer.bot dlls/).
#if EXTERNAL_EDITOR
public class FunCommandsMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "Fun Commands";
    private const string Version = "1.0.0";
    private static readonly Random Random = new Random();
    private sealed class CommandConfig
    {
        public string Name;
        public string Template;
        public string TargetMode;
        public List<VarRow> Vars = new List<VarRow>();
    }

    private sealed class VarRow
    {
        public string Name;
        public int Min;
        public int Max;
        public string[] Values;
    }

    private sealed class Viewer
    {
        public string Id { get; set; }
        public string UserName { get; set; }
    }

    private sealed class Settings
    {
        public string[] ExcludeGroups { get; set; } = new[]
        {
            "excludeFromRandom"
        };
        public bool ExcludeBroadcaster { get; set; } = true;
        public bool ExcludeBot { get; set; } = true;
        public bool ReplyUnknown { get; set; }
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();
    }

    public bool Execute()
    {
        var log = Fc.Logger(CPH, Title, Version);
        var settings = LoadSettings();
        var ev = Fc.CaptureEvent(CPH);
        if (!TryMatchCommand(ev, settings, out var config, out var leftoverInput))
        {
            if (settings.ReplyUnknown)
                ReplyToMessage("Unknown command.", ev);
            else
                log.Info("No matching command.");
            return true;
        }

        var user = MessageTemplates.SanitizeMention(!string.IsNullOrWhiteSpace(ev.User) ? ev.User : ev.UserName);
        if (string.IsNullOrWhiteSpace(user))
        {
            log.Warn("No user argument; skip.");
            return true;
        }

        var template = config.Template ?? "";
        var target = user;
        if (TemplateUsesTarget(template))
        {
            target = DetermineTarget(user, leftoverInput, config, settings, ev.UserId, ev);
            if (target == null)
                return true;
        }

        var vars = BuildTemplateVariables(user, target, config);
        if (!TryAssignRandomUsers(template, vars, settings, ev.UserId, ev))
            return true;
        ReplyToMessage(Fc.ApplyTemplate(NormalizeInvokerTemplate(template), vars), ev);
        return true;
    }

    private static string NormalizeInvokerTemplate(string template)
    {
        if (string.IsNullOrEmpty(template))
            return template;
        template = template.Replace("@{user}", "{user}");
        if (template.StartsWith("{user}, ", StringComparison.Ordinal))
            template = template.Substring("{user}, ".Length);
        return template;
    }

    private void ReplyToMessage(string message, EventContext ev)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        if (!string.IsNullOrWhiteSpace(ev.MessageId))
            CPH.TwitchReplyToMessage(message, ev.MessageId);
        else
            CPH.SendMessage(message);
    }

    private Settings LoadSettings()
    {
        var settings = new Settings
        {
            ExcludeGroups = LoadExcludeGroups(),
            ExcludeBroadcaster = Fc.GetSetting(CPH, Title, "exclude_broadcaster", true),
            ExcludeBot = Fc.GetSetting(CPH, Title, "exclude_bot", true),
            ReplyUnknown = Fc.GetSetting(CPH, Title, "reply_unknown", false),
        };
        string[] names = Fc.GetSetting(CPH, Title, "commands", Array.Empty<string>());
        if (names == null || names.Length == 0)
        {
            if (!Fc.HasSavedSettings(CPH, Title))
            {
                foreach (var builtIn in BuiltIns.All())
                    settings.Commands.Add(FromBuiltIn(builtIn));
            }

            EnsureBuiltIns(settings);
            return settings;
        }

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (!Fc.GetSetting(CPH, Title, name + "_enabled", true))
                continue;
            var cmd = new CommandConfig
            {
                Name = name.Trim(),
                Template = Fc.GetSetting(CPH, Title, name + "_template", "") ?? "",
                TargetMode = (Fc.GetSetting(CPH, Title, name + "_target_mode", "self") ?? "self").Trim(),
                Vars = ParseVars(Fc.GetSetting(CPH, Title, name + "_vars", new JArray())),
            };
            settings.Commands.Add(cmd);
            var aliases = Fc.GetSetting(CPH, Title, name + "_aliases", Array.Empty<string>());
            if (aliases == null)
                continue;
            foreach (var alias in aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;
                settings.Commands.Add(new CommandConfig { Name = alias.Trim(), Template = cmd.Template, TargetMode = cmd.TargetMode, Vars = cmd.Vars, });
            }
        }

        EnsureBuiltIns(settings);
        return settings;
    }

    private static void EnsureBuiltIns(Settings settings)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cmd in settings.Commands)
        {
            if (cmd == null || string.IsNullOrWhiteSpace(cmd.Name))
                continue;
            var key = Normalize(cmd.Name);
            if (key.Length > 0)
                existing.Add(key);
        }

        foreach (var builtIn in BuiltIns.All())
        {
            var key = Normalize(builtIn.Name);
            if (key.Length == 0 || existing.Contains(key))
                continue;
            settings.Commands.Add(FromBuiltIn(builtIn));
            existing.Add(key);
        }
    }

    private string[] LoadExcludeGroups()
    {
        var groups = Fc.GetSetting(CPH, Title, "exclude_groups", Array.Empty<string>());
        if (groups != null && groups.Length > 0)
            return groups.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).ToArray();
        var legacy = Fc.GetSetting(CPH, Title, "exclude_group", "");
        if (!string.IsNullOrWhiteSpace(legacy))
        {
            var migrated = new[]
            {
                legacy.Trim()
            };
            Fc.SaveSettings(CPH, Title, o =>
            {
                o["exclude_groups"] = new JArray(migrated);
                o.Remove("exclude_group");
            });
            return migrated;
        }

        return Array.Empty<string>();
    }

    private static CommandConfig FromBuiltIn(BuiltIns.Command src)
    {
        var vars = new List<VarRow>();
        if (src.Vars != null)
        {
            foreach (var row in src.Vars)
            {
                vars.Add(new VarRow { Name = row.name, Min = row.min, Max = row.max, Values = row.values, });
            }
        }

        return new CommandConfig
        {
            Name = src.Name,
            Template = src.Template,
            TargetMode = src.TargetMode,
            Vars = vars,
        };
    }

    private static List<VarRow> ParseVars(JArray arr)
    {
        var list = new List<VarRow>();
        if (arr == null)
            return list;
        foreach (var token in arr)
        {
            var obj = token as JObject;
            if (obj == null)
                continue;
            var name = (obj["name"]?.ToString() ?? "").Trim();
            if (name.Length == 0)
                continue;
            var values = (obj["values"] as JArray)?.Select(t => t?.ToString() ?? "").Where(s => s.Length > 0).ToArray();
            int min = obj["min"]?.Value<int>() ?? 0;
            int max = obj["max"]?.Value<int>() ?? 100;
            list.Add(new VarRow { Name = name, Min = min, Max = max, Values = values, });
        }

        return list;
    }

    private static bool TryMatchCommand(EventContext ev, Settings settings, out CommandConfig config, out string leftoverInput)
    {
        config = null;
        leftoverInput = "";
        var tokens = new List<string>();
        AddWords(tokens, ev.Command);
        if (tokens.Count == 0)
            AddWords(tokens, TriggerCommand(ev.TriggerName));
        if (tokens.Count == 0)
            AddWords(tokens, LeadingBangCommand(ev.Message));
        AddWords(tokens, ev.RawInput);
        if (tokens.Count == 0)
            return false;
        var lookup = new Dictionary<string, CommandConfig>(StringComparer.Ordinal);
        foreach (var cmd in settings.Commands)
        {
            if (cmd == null || string.IsNullOrWhiteSpace(cmd.Name))
                continue;
            var key = Normalize(cmd.Name);
            if (key.Length == 0)
                continue;
            lookup[key] = cmd;
        }

        if (lookup.Count == 0)
            return false;
        for (int take = tokens.Count; take >= 1; take--)
        {
            var key = Normalize(string.Join(" ", tokens.Take(take)));
            if (key.Length == 0 || !lookup.TryGetValue(key, out config))
                continue;
            leftoverInput = string.Join(" ", tokens.Skip(take));
            return true;
        }

        return false;
    }

    private static string TriggerCommand(string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
            return "";
        var s = triggerName.Trim();
        if (s.StartsWith("!", StringComparison.Ordinal))
            return s;
        return "";
    }

    private static string LeadingBangCommand(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "";
        var s = message.Trim();
        if (!s.StartsWith("!", StringComparison.Ordinal))
            return "";
        int space = s.IndexOf(' ');
        return space < 0 ? s : s.Substring(0, space);
    }

    private static void AddWords(List<string> tokens, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        foreach (var part in text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = part.Trim().TrimStart('!');
            if (word.Length > 0)
                tokens.Add(word);
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private string DetermineTarget(string user, string leftoverInput, CommandConfig config, Settings settings, string userId, EventContext ev)
    {
        var specified = leftoverInput.Replace("@", "").Trim();
        if (string.IsNullOrEmpty(specified))
        {
            if (string.Equals(config.TargetMode, "random", StringComparison.OrdinalIgnoreCase))
                return PickRandomViewer(user, userId, settings, ev);
            return user;
        }

        if (IsRandomKeyword(specified))
            return PickRandomViewer(user, userId, settings, ev);
        if (IsSelfKeyword(specified))
            return user;
        var valid = CPH.TwitchGetUserInfoByLogin(specified)?.UserName;
        if (string.IsNullOrEmpty(valid))
        {
            ReplyToMessage($"the user '{specified}' does not exist.", ev);
            return null;
        }

        return valid;
    }

    private static bool IsRandomKeyword(string value)
    {
        return string.Equals(value, "random", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "r", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelfKeyword(string value)
    {
        return string.Equals(value, "me", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "myself", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TemplateUsesTarget(string template)
    {
        return !string.IsNullOrEmpty(template) && template.IndexOf("{target", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool TryAssignRandomUsers(string template, Dictionary<string, string> vars, Settings settings, string invokerId, EventContext ev)
    {
        var keys = CollectRandomUserKeys(template);
        if (keys.Count == 0)
            return true;
        var viewers = GetEligibleViewers(settings, invokerId);
        if (viewers == null || viewers.Count == 0)
        {
            ReplyToMessage("no other viewers found!", ev);
            return false;
        }

        foreach (var key in keys)
        {
            if (vars.ContainsKey(key))
                continue;
            vars[key] = viewers[Random.Next(viewers.Count)].UserName;
        }

        return true;
    }

    private static List<string> CollectRandomUserKeys(string template)
    {
        var keys = new List<string>();
        if (string.IsNullOrEmpty(template))
            return keys;
        int i = 0;
        while (i < template.Length)
        {
            int start = template.IndexOf("{random_user", i, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                break;
            int end = template.IndexOf('}', start + 1);
            if (end < 0)
                break;
            var key = template.Substring(start + 1, end - start - 1);
            if (IsRandomUserKey(key) && !keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                keys.Add(key);
            i = end + 1;
        }

        return keys;
    }

    private static bool IsRandomUserKey(string key)
    {
        const string prefix = "random_user";
        if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        if (key.Length == prefix.Length)
            return true;
        for (int i = prefix.Length; i < key.Length; i++)
        {
            if (!char.IsDigit(key[i]))
                return false;
        }

        return true;
    }

    private string PickRandomViewer(string user, string userId, Settings settings, EventContext ev)
    {
        var randomUser = GetRandomViewer(settings, userId);
        if (randomUser == null)
        {
            ReplyToMessage("no other viewers found!", ev);
            return null;
        }

        return randomUser;
    }

    private static Dictionary<string, string> BuildTemplateVariables(string user, string targetUser, CommandConfig config)
    {
        var isSelf = string.Equals(targetUser, user, StringComparison.OrdinalIgnoreCase);
        var mention = isSelf ? "you" : "@" + targetUser;
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["user"] = user,
            ["target"] = isSelf ? "you are" : "@" + targetUser + " is",
            ["target_direct"] = isSelf ? "themselves" : "@" + targetUser,
            ["target_possessive"] = isSelf ? "your" : "@" + targetUser + "'s",
            ["target_name"] = targetUser,
            ["target_mention"] = mention,
        };
        if (config.Vars == null)
            return variables;
        foreach (var variable in config.Vars)
        {
            if (variable == null || string.IsNullOrWhiteSpace(variable.Name))
                continue;
            variables[variable.Name] = GenerateVariable(variable);
        }

        return variables;
    }

    private static string GenerateVariable(VarRow variable)
    {
        if (variable.Values != null && variable.Values.Length > 0)
            return variable.Values[Random.Next(variable.Values.Length)];
        int min = variable.Min;
        int max = variable.Max;
        if (min > max)
        {
            int tmp = min;
            min = max;
            max = tmp;
        }

        return Random.Next(min, max + 1).ToString();
    }

    private string GetRandomViewer(Settings settings, string invokerId)
    {
        var viewers = GetEligibleViewers(settings, invokerId);
        if (viewers == null || viewers.Count == 0)
            return null;
        return viewers[Random.Next(viewers.Count)].UserName;
    }

    private List<Viewer> GetEligibleViewers(Settings settings, string invokerId)
    {
        var json = CPH.GetGlobalVar<string>("currentViewers", false);
        if (string.IsNullOrEmpty(json))
        {
            Fc.Logger(CPH, Title, Version).Warn("No currentViewers data available.");
            return null;
        }

        List<Viewer> viewers;
        try
        {
            viewers = JsonConvert.DeserializeObject<List<Viewer>>(json);
        }
        catch (Exception ex)
        {
            Fc.Logger(CPH, Title, Version).Failed("deserializing currentViewers", ex);
            return null;
        }

        if (viewers == null)
            return null;
        if (!string.IsNullOrEmpty(invokerId))
            viewers.RemoveAll(x => x.Id == invokerId);
        if (settings.ExcludeBroadcaster)
        {
            var broadcaster = CPH.TwitchGetBroadcaster();
            if (broadcaster != null)
            {
                viewers.RemoveAll(x => (!string.IsNullOrEmpty(broadcaster.UserId) && x.Id == broadcaster.UserId) || string.Equals(x.UserName, broadcaster.UserName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (settings.ExcludeBot)
        {
            viewers.RemoveAll(x => KnownBots.IsKnownBot(x.UserName));
            var bot = CPH.TwitchGetBot();
            if (bot != null)
            {
                viewers.RemoveAll(x => (!string.IsNullOrEmpty(bot.UserId) && x.Id == bot.UserId) || string.Equals(x.UserName, bot.UserName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (settings.ExcludeGroups != null)
        {
            var excludeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in settings.ExcludeGroups)
            {
                if (string.IsNullOrWhiteSpace(group))
                    continue;
                var groupUsers = CPH.UsersInGroup(group.Trim());
                if (groupUsers == null || groupUsers.Count == 0)
                    continue;
                foreach (var groupUser in groupUsers)
                {
                    if (!string.IsNullOrEmpty(groupUser.Id))
                        excludeIds.Add(groupUser.Id);
                }
            }

            if (excludeIds.Count > 0)
                viewers.RemoveAll(x => excludeIds.Contains(x.Id));
        }

        if (viewers.Count == 0)
        {
            Fc.Logger(CPH, Title, Version).Info("No eligible viewers found.");
            return null;
        }

        return viewers;
    }

    private static class BuiltIns
    {
        public sealed class Command
        {
            public string Name;
            public string Template;
            public string TargetMode;
            public VarRow[] Vars;
        }

        public sealed class VarRow
        {
            public string name { get; set; }
            public int min { get; set; }
            public int max { get; set; }
            public string[] values { get; set; }
        }

        public static Command[] All() => new[]
        {
            Cmd("goodgirl", "{target} {percent}% goodgirl????", "self", Range("percent", 0, 100)),
            Cmd("goodboy", "{target} {percent}% goodboy ????", "self", Range("percent", 0, 100)),
            Cmd("sweat", "{target} playing at a sweat level of {percent}% ????", "self", Range("percent", 0, 100)),
            Cmd("stinky", "{target} {percent}% stinky today! ???? Time for a shower! ??", "self", Range("percent", 0, 100)),
            Cmd("pp", "{target_possessive} pickle size is {size1}.{size2} inches ??????", "self", Range("size1", 0, 10), Range("size2", 0, 9)),
            Cmd("pro", "{target} {skill}! ??????", "self", List("skill", "a Fortnite pro", "a bot", "cracked", "washed", "the next World Cup champ", "a default skin at heart", "absolutely goated", "built different", "lowkey carry", "the clutch king", "straight up cracked", "underrated af")),
            Cmd("cringe", "{target} just did something {action}! ??????", "self", List("action", "cringe", "epic", "totally normal", "worthy of a TikTok", "that made the lobby laugh", "that made the lobby cry", "absolutely unhinged", "main character energy", "NPC behavior", "straight up cursed")),
            Cmd("hug", "{user} gives {target_direct} a {hug_type} hug! ??????", "random", List("hug_type", "big warm", "gentle", "tight", "comforting", "loving", "friendly", "awkward", "surprise")),
            Cmd("kiss", "{user} gives {target_direct} a {kiss_type} kiss! ??????", "random", List("kiss_type", "sweet", "virtual", "on-the-cheek", "forehead", "gentle", "butterfly", "air", "dramatic", "speedy", "single-peck", "surprise", "bouncy", "mwah", "French", "Eskimo", "spidey upside-down", "cheeky", "shy", "midnight", "sleepy", "glam")),
            Cmd("spank", "{user} spanks {target_direct}! ???? {reaction} ??", "random", List("reaction", "That's what you get!", "Ouch!", "Deserved!", "Kinky!", "Harder daddy!", "Stop it!", "Again!")),
            Cmd("simp", "{user} is simping for {target_direct} in chat! ??????", "random"),
            Cmd("ratio", "{user} just got ratio'd by {target_direct}! Better luck next time! #ratio ????", "random"),
            Cmd("shiton", "{user} shit on {target_direct} ????????", "random"),
            Cmd("fmk", "Fuck: @{random_user} ?? | Marry: @{random_user2} ?? | Kill: @{random_user3} ??", "self"),
        };
        private static Command Cmd(string name, string template, string targetMode, params VarRow[] vars) => new Command
        {
            Name = name,
            Template = template,
            TargetMode = targetMode,
            Vars = vars
        };
        private static VarRow Range(string name, int min, int max) => new VarRow
        {
            name = name,
            min = min,
            max = max
        };
        private static VarRow List(string name, params string[] values) => new VarRow
        {
            name = name,
            min = 0,
            max = 100,
            values = values
        };
    }
}