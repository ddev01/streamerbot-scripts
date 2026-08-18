using FluentConfig;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

// Refs: PresentationFramework, PresentationCore, WindowsBase,
//       FluentConfig.dll (Streamer.bot dlls/).
// Execute C# Method + Run on UI thread.
#if EXTERNAL_EDITOR
public class FunCommandsSettings : CPHInlineBase
#else
public class CPHInline
#endif
{
    private static class ExtensionInfo
    {
        public const string Title = "Fun Commands";
        public const string Version = "1.0.0";
        public const string Repo = "ddev01/streamerbot-scripts";
        public const string TagPrefix = "fun-commands";
    }

    public bool Execute()
    {
        CPH.LogInfo($"[Fun Commands] Opening settings ({ExtensionInfo.Title} v{ExtensionInfo.Version}).");
        if (!Fc.HasSavedSettings(CPH, ExtensionInfo.Title))
            Fc.SaveSettings(CPH, ExtensionInfo.Title, SeedBuiltIns);
        else
            Fc.SaveSettings(CPH, ExtensionInfo.Title, MergeMissingBuiltIns);
        Fc.Open(CPH, ExtensionInfo.Title, ExtensionInfo.Version, ui => ui
                .WithExtensionUpdateNotice(ExtensionInfo.Repo, ExtensionInfo.Version, tagPrefix: ExtensionInfo.TagPrefix)
                .Section("General", "General", g => g
                    .Intro("All Streamer.bot **command triggers** can point at the same main action. " + "Add a pill here, then add a matching trigger - or skip the extra trigger when a shorter " + "parent already fires (e.g. `!pro` + `coder` in chat resolves to **pro coder**).\n\n" + "Placeholders:\n" + "- `{target}` - `you are` / `@name is` (meter commands usually start with this)\n" + "- `{target_direct}` - `themselves` / `@name`\n" + "- `{target_possessive}` - `your` / `@name's`\n" + "- `{user}` - invoker name only if you need it (no `@`; reply already points at them)\n" + "- `{random_user}` `{random_user2}` `{random_user3}` - random viewers (duplicates allowed)\n" + "- plus any variable name you add, e.g. `{percent}` `{skill}`\n\n" + "Example **gay** template: `{target} {percent}% fruity (gay af) ????????`\n\n" + "Targeting: no extra words uses **Default target**. " + "`@user` always targets that person. " + "`random` / `r` / `@random` / `@r` picks a viewer. " + "`me` / `@me` is always the invoker.")
                    .Grid("grid-cols-2 items-center", row => row
                        .Toggle("Exclude broadcaster", "exclude_broadcaster")
                            .Hint("Skip the channel owner when picking a random viewer.")
                            .Default(true)
                        .Toggle("Exclude known bots", "exclude_bot")
                            .Hint("Skip known chat bots and the connected Twitch bot account.")
                            .Default(true))
                    .Dropdown("Exclude groups", "exclude_groups")
                        .Hint("Streamer.bot user groups skipped for random targets. Refresh loads groups from this install.")
                        .Searchable()
                        .AllowCustom()
                        .Multiple()
                        .Refresh(ListUserGroups)
                        .Default(new[] { "excludeFromRandom" })
                    .Toggle("Reply when command is unknown", "reply_unknown")
                        .Hint("Off = skip quietly (logged). On = send 'Unknown command.'")
                        .Default(false))
                .Section("Commands", "Commands", c => c
                    .Intro("Pill **name** is the command (`gay`, `pro coder`). " + "If **Pick from list** has entries, one is chosen at random; otherwise **Min/Max** is a random integer.")
                    .PillInput("Commands", "commands")
                        .Hint("Type a name and press Enter. Example: pro coder")
                        .ItemTemplate(item => item
                            .Title("Command: {name}")
                            .Toggle("Enabled", "{name}_enabled")
                                .Default(true)
                            .Textbox("Template", "{name}_template")
                                .Hint("e.g. {target} {percent}% fruity . - omit {user}; replies already point at the invoker.")
                                .Multiline()
                            .Dropdown("Default target", "{name}_target_mode")
                                .Hint("Used when chat has no extra words. `@user`, `random`/`r`, and `me` still override.")
                                .Options(new[] { ("self", "Self"), ("random", "Random viewer") })
                                .DefaultByValue("self")
                            .DynamicTextboxes("Aliases", "{name}_aliases")
                                .Hint("Extra names that resolve to this command (procoder, pro-coder, .).")
                                .AllowDuplicates(false)
                            .WithRepeatableRows("{name}_vars", row => row
                                .Textbox("Variable", "name")
                                    .Hint("Placeholder without braces. percent  {percent}")
                                .Grid("grid-cols-2 items-center", nums => nums
                                    .IntegerInput("Min", "min")
                                        .Range(0, 1000000)
                                        .Default(0)
                                        .Size("w-fit min-w-16")
                                    .IntegerInput("Max", "max")
                                        .Range(0, 1000000)
                                        .Default(100)
                                        .Size("w-fit min-w-16"))
                                .DynamicTextboxes("Pick from list", "values")
                                    .Hint("If this list is not empty, a random entry is used instead of min/max.")
                                    .AllowDuplicates(true)))
                        .OnPillRemoved((name, ctx) =>
        {
            ctx.RemoveSettingsKeys(name + "_enabled", name + "_template", name + "_target_mode", name + "_aliases", name + "_vars");
        })));
        return true;
    }

    private string[] ListUserGroups()
    {
        var groups = CPH.GetGroups();
        if (groups == null || groups.Count == 0)
            return Array.Empty<string>();
        return groups.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void SeedBuiltIns(JObject o)
    {
        o["exclude_groups"] = new JArray("excludeFromRandom");
        o["exclude_broadcaster"] = true;
        o["exclude_bot"] = true;
        o["reply_unknown"] = false;
        var names = new JArray();
        foreach (var cmd in BuiltIns.All())
        {
            names.Add(cmd.Name);
            WriteCommand(o, cmd);
        }

        o["commands"] = names;
    }

    private static void MergeMissingBuiltIns(JObject o)
    {
        var names = o["commands"] as JArray ?? new JArray();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in names)
        {
            var name = (token?.ToString() ?? "").Trim();
            if (name.Length > 0)
                existing.Add(name);
        }

        foreach (var cmd in BuiltIns.All())
        {
            if (existing.Contains(cmd.Name))
                continue;
            names.Add(cmd.Name);
            WriteCommand(o, cmd);
        }

        o["commands"] = names;
    }

    private static void WriteCommand(JObject o, BuiltIns.Command cmd)
    {
        o[cmd.Name + "_enabled"] = true;
        o[cmd.Name + "_template"] = cmd.Template;
        o[cmd.Name + "_target_mode"] = cmd.TargetMode;
        o[cmd.Name + "_aliases"] = new JArray();
        o[cmd.Name + "_vars"] = JArray.FromObject(cmd.Vars ?? Array.Empty<BuiltIns.VarRow>());
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
            Cmd("fmk", "{user} wants to fuck: @{random_user} ?? | Marry: @{random_user2} ?? | Kill: @{random_user3} ??", "self"),
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