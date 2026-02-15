using FluentConfig;
using FluentConfig.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

public class CPHInline
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
            
            .Show();

        return true;
    }
}