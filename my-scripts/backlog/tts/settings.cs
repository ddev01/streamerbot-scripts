using FluentConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

#if EXTERNAL_EDITOR
public class TtsSettings : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "TTS Sell System";
    private const string Version = "1.0";
    private const string SettingsKey = "FluentConfig_Settings_TTS Sell System";

    public bool Execute()
    {
        if (FluentConfig.FluentConfig.AlreadyOpened(Title, Version))
            return true;

        var voicePanels = new Dictionary<string, Panel>();

        void AddVoiceSection(Panel container, string alias, CallbackContext ctx)
        {
            ctx.WithPanel(container, "Voices", pb => pb
                .Title($"Voice: {alias}")
                .WithRepeatableRows(alias + "_prices", row => row
                    .Grid(2, g => g
                        .IntegerInput("Price", "price")
                            .Hint("Points cost for this tier.")
                            .Range(0, 1000000)
                            .Default(100)
                        .DurationInput("Duration", "duration")
                            .Hint("How long the voice lasts. Permanent = forever.")
                            .WithPermanentOption(true)
                            .Default("permanent")
                    )
                )
            );
        }

        string[] GetVoiceAliasesFromSettings()
        {
            try
            {
                var json = CPH.GetGlobalVar<string>(SettingsKey, true);
                if (string.IsNullOrEmpty(json)) return Array.Empty<string>();
                var obj = JObject.Parse(json);
                var arr = obj["tts_voice_aliases"] as JArray;
                return arr?.Select(t => t?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray() ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        FluentConfigUi.Create(CPH, Title, Version)
            .Section("General", "General", s => s
                .Intro("General settings for the TTS system.")
                .Input("Points variable name", "points_variable_name")
                    .Hint("The name of the variable that will store the points cost for the voice.")
                    .Default("points")
                .Toggle("TTS costs points", "tts_costs_points")
                    .Hint("Whether the TTS costs points or not.")
                    .Default(true)
                .WithVisibility("tts_costs_points", (PanelBuilder v) => v
                    .Intro("Choose how to charge: flat price, per character, or both.")
                    .Dropdown("Pricing mode", "pricing_mode")
                        .Hint("Flat = fixed cost per message. Per character = cost scales with message length. Both = flat fee plus per-character.")
                        .OptionsPairs(new[] { ("flat", "Flat only"), ("per_char", "Per character only"), ("both", "Flat + per character") })
                        .WithPairValue("pricing_mode_value")
                        .DefaultByValue("flat")
                    .Flex(f => f
                        .NumberInput("Flat price (points)", "default_voice_flat_price")
                            .Hint("Fixed cost per TTS message. Used when mode is 'flat' or 'both'.")
                            .Range(0, double.MaxValue)
                            .Default(100)
                            .ShowWhen(new[] { "pricing_mode_value" }, ctx =>
                                ctx.GetPendingValue<string>("pricing_mode_value") == "flat" ||
                                ctx.GetPendingValue<string>("pricing_mode_value") == "both")
                        .NumberInput("Points per character", "default_voice_per_char_price")
                            .Hint("Additional cost per character. Used when mode is 'per_char' or 'both'.")
                            .Range(0, double.MaxValue)
                            .Default(1)
                            .ShowWhen(new[] { "pricing_mode_value" }, ctx =>
                                ctx.GetPendingValue<string>("pricing_mode_value") == "per_char" ||
                                ctx.GetPendingValue<string>("pricing_mode_value") == "both")
                    )
                )
            )
            .Section("Voices", "Voices", s => s
                .Intro("Add voice aliases and set point prices. Each alias can have multiple tiers (price + duration).")
                .PillInput("Voice aliases", "tts_voice_aliases")
                    .Hint("Add voice names (e.g. french, english, german). Each gets pricing tiers below.")
                    .WithSectionsPanel((sections, tab, ctx) =>
                    {
                        tab.Children.Add(sections);
                        var items = ctx.GetValue<string[]>("tts_voice_aliases") ?? Array.Empty<string>();
                        foreach (var item in items)
                        {
                            var wrapper = new StackPanel();
                            sections.Children.Add(wrapper);
                            voicePanels[item] = wrapper;
                            AddVoiceSection(wrapper, item, ctx);
                        }
                    })
                    .OnPillAdded((alias, sections, ctx) =>
                    {
                        var wrapper = new StackPanel();
                        sections.Children.Add(wrapper);
                        voicePanels[alias] = wrapper;
                        AddVoiceSection(wrapper, alias, ctx);
                    })
                    .OnPillRemoved((alias, sections, ctx) =>
                    {
                        if (voicePanels.TryGetValue(alias, out var wrapper))
                        {
                            sections.Children.Remove(wrapper);
                            voicePanels.Remove(alias);
                            ctx.RemoveSettingsKeys(alias + "_prices");
                        }
                    })
                .Dropdown("Default voice alias", "default_voice_alias_display")
                    .Hint("Voice for users who haven't purchased. Click Refresh to update from current aliases (no save needed).")
                    .OptionsPairs(GetVoiceAliasesFromSettings().Select(a => (a, a)).ToArray())
                    .WithPairValue("default_voice_alias")
                    .Refresh(ctx =>
                    {
                        var aliases = ctx.GetPendingValue<string[]>("tts_voice_aliases") ?? Array.Empty<string>();
                        return aliases.Select(a => (a, a)).ToArray();
                    })
                    .DefaultByValue(GetVoiceAliasesFromSettings().FirstOrDefault() ?? "")
            )
            .Section("Anti-Spam", "Anti-Spam", s => s
                .Intro("Protect TTS from spam. Messages that match enabled rules are blocked before costing points.")
                .Toggle("Enable anti-spam", "antispam_enabled")
                    .Hint("Master switch for all anti-spam checks.")
                    .Default(true)
                .WithVisibility("antispam_enabled", (PanelBuilder v) => v
                    .Toggle("Repetitive phrase detection", "antispam_repetitive_phrase")
                        .Hint("Blocks messages like 'word1 word2 word1 word2 word1 word2' (same phrase repeating).")
                        .Default(true)
                    .WithVisibility("antispam_repetitive_phrase", false, (PanelBuilder v2) => v2
                        .Flex(f => f
                            .IntegerInput("Pattern length (words)", "antispam_repetitive_pattern_len")
                                .Hint("Length of the phrase to check (e.g. 2 = 'sybau streamer').")
                                .Range(1, 10)
                                .Default(2)
                            .IntegerInput("Min repeats to block", "antispam_repetitive_min_repeats")
                                .Hint("Block if phrase repeats this many times.")
                                .Range(2, 20)
                                .Default(3)
                        )
                    )
                    .Toggle("Character repetition detection", "antispam_char_repeat")
                        .Hint("Blocks messages with same character repeated (e.g. 'aaaaaa', '!!!!!!').")
                        .Default(true)
                    .WithVisibility("antispam_char_repeat", false, (PanelBuilder v2) => v2
                        .IntegerInput("Max same character streak", "antispam_char_repeat_max")
                            .Hint("Block if any character repeats more than this.")
                            .Range(2, 20)
                            .Default(5)
                    )
                    .Toggle("Uniqueness ratio check", "antispam_uniqueness_ratio")
                        .Hint("Blocks when too few unique words (e.g. 2 unique in 12 words = spam).")
                        .Default(true)
                    .WithVisibility("antispam_uniqueness_ratio", false, (PanelBuilder v2) => v2
                        .NumberInput("Min unique word ratio", "antispam_min_uniqueness")
                            .Hint("0.4 = at least 40% of words must be unique.")
                            .Range(0.1, 1.0)
                            .Default(0.4)
                    )
                    .Toggle("Max message length", "antispam_max_length")
                        .Hint("Block messages over a character limit.")
                        .Default(true)
                    .WithVisibility("antispam_max_length", false, (PanelBuilder v2) => v2
                        .IntegerInput("Max characters", "antispam_max_length_chars")
                            .Hint("Maximum allowed message length.")
                            .Range(50, 2000)
                            .Default(500)
                    )
                )
            )
            
            .Show();

        return true;
    }
}