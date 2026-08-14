using FluentConfig;

// Refs: PresentationFramework, PresentationCore, WindowsBase,
//       FluentConfig.dll (Streamer.bot dlls/).
// Execute C# Method + Run on UI thread.
#if EXTERNAL_EDITOR
public class FiresaleSettings : CPHInlineBase
#else
public class CPHInline
#endif
{
    private static class ExtensionInfo
    {
        public const string Title = "Fire Sale";
        public const string Version = "1.0.0";
        public const string Repo = "ddev01/streamerbot-scripts";
        public const string TagPrefix = "firesale";
    }

    public bool Execute()
    {
        CPH.LogInfo($"[Fire Sale] Opening settings ({ExtensionInfo.Title} v{ExtensionInfo.Version}).");
        Fc.Open(CPH, ExtensionInfo.Title, ExtensionInfo.Version, ui => ui
                .WithExtensionUpdateNotice(ExtensionInfo.Repo, ExtensionInfo.Version, tagPrefix: ExtensionInfo.TagPrefix)
                .Section("General", "General", s => s
                    .Intro("Discount (or free) channel point rewards for a while, then restore original prices.\n\n" + "`!firesale` uses these defaults. Override with `0.5` / `50%`, `random`, `random 0.2-0.8`, or `free`, " + "and an optional duration (`30m`, `1h`, `permanent`). `status` and `off` also work.")
                    .Dropdown("Default mode", "default_mode")
                        .Hint("Used when the command has no mode argument.")
                        .Options(new[] { ("0", "Fixed multiplier"), ("1", "Random multiplier"), ("2", "Free (1 point)"), })
                        .DefaultByValue("0")
                    .WithVisibility("default_mode", Comparator.LessOrEqual, 0, inner => inner
                        .NumberInput("Price multiplier", "default_multiplier")
                            .Hint("0.5 = half of the original cost.")
                            .Range(0.01, 1)
                            .Step(0.05)
                            .Default(0.5)
                            .Size("w-fit min-w-24"))
                    .WithVisibility("default_mode", Comparator.GreaterOrEqual, 1, inner => inner
                        .WithVisibility("default_mode", Comparator.LessOrEqual, 1, random => random
                            .Grid("grid-cols-2 items-center", g => g
                                .NumberInput("Random min", "random_min")
                                    .Hint("Lowest multiplier for this sale.")
                                    .Range(0.01, 1)
                                    .Step(0.05)
                                    .Default(0.1)
                                    .Size("w-fit min-w-24")
                                .NumberInput("Random max", "random_max")
                                    .Hint("Highest multiplier for this sale.")
                                    .Range(0.01, 1)
                                    .Step(0.05)
                                    .Default(0.9)
                                    .Size("w-fit min-w-24"))))
                    .DurationInput("Default duration", "default_duration")
                        .Hint("How long the sale lasts if the command omits a duration. Permanent = until !firesale off.")
                        .WithPermanentOption(true)
                        .Default("permanent")
                    .Textbox("Auto-end timer Id", "timer_id")
                        .Hint("Timer that runs the End action. Needed so timed sales can auto-end.")
                        .Size("w-full")
                    .Toggle("Replace an active sale", "replace_if_active")
                        .Hint("If on, starting a new sale restores original prices first. If off, chat is told to use !firesale off.")
                        .Default(false))
                .Section("Rewards", "Rewards", r => r
                    .Intro("Leave include empty to affect every channel point reward. Exclude always wins. " + "Names match the **Group** field on each reward in Streamer.bot (case-insensitive).")
                    .Textbox("Include groups", "include_groups")
                        .Hint("Comma-separated reward groups. Empty = all groups.")
                        .Default("")
                    .Textbox("Exclude groups", "exclude_groups")
                        .Hint("Comma-separated groups to skip (even if listed under include).")
                        .Default(""))
                .Section("Messages", "Messages", m => m
                    .Intro("Placeholders: `{count}` `{groups}` `{mode}` `{discount}` `{multiplier}` `{duration}` `{remaining}`.\n\n" + "`{discount}` is percent off (50 when prices are halved). `{remaining}` is only useful on status.")
                    .Toggle("Send chat messages", "send_message")
                        .Hint("When off, prices still change and arguments are still set for sub-actions.")
                        .Default(true)
                    .WithVisibility("send_message", inner => inner
                        .Textbox("Activating", "msg_processing")
                            .Multiline()
                            .Default("?? Fire sale activating! Updating {count} rewards ({mode})...")
                        .Textbox("Started", "msg_started")
                            .Multiline()
                            .Default("? Fire sale is ACTIVE! {mode} on {groups} ({count} rewards){duration}")
                        .WithVisibilityWhenOff("replace_if_active", blocked => blocked
                            .Textbox("Already active", "msg_already_active")
                                .Multiline()
                                .Default("?? Fire sale is already active! Use !firesale off first."))
                        .Textbox("Status (on)", "msg_status_on")
                            .Multiline()
                            .Default("?? Fire sale is ACTIVE! {mode} on {groups} ({count} rewards){remaining}")
                        .Textbox("Status (off)", "msg_status_off")
                            .Multiline()
                            .Default("?? Fire sale is currently OFF.")
                        .Textbox("Ended", "msg_ended")
                            .Multiline()
                            .Default("? Fire sale ended! Restored {count} rewards to original prices.")
                        .Textbox("Timer ended", "msg_auto_ended")
                            .Multiline()
                            .Default("? Fire sale ended automatically! Restored {count} rewards to original prices.")
                        .Textbox("No rewards", "msg_no_rewards")
                            .Multiline()
                            .Default("No channel point rewards matched the configured groups."))));
        return true;
    }
}