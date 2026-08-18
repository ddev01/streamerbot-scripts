using FluentConfig;
using Newtonsoft.Json.Linq;
using System;

// Refs: PresentationFramework, PresentationCore, WindowsBase,
//       FluentConfig.dll (Streamer.bot dlls/)
// Execute C# Method + Run on UI thread.
#if EXTERNAL_EDITOR
public class RefundRewardsSettings : CPHInlineBase
#else
public class CPHInline
#endif
{
    private static class ExtensionInfo
    {
        public const string Title = "Refund Rewards";
        public const string Version = "1.0.0";
        public const string Repo = "ddev01/streamerbot-scripts";
        public const string TagPrefix = "refund-rewards";
    }

    public bool Execute()
    {
        Fc.Logger(CPH, ExtensionInfo.Title, ExtensionInfo.Version).Info("Opening settings.");
        if (!Fc.HasSavedSettings(CPH, ExtensionInfo.Title))
            Fc.SaveSettings(CPH, ExtensionInfo.Title, SeedDefaults);
        Fc.Open(CPH, ExtensionInfo.Title, ExtensionInfo.Version, ui => ui
                .WithExtensionUpdateNotice(ExtensionInfo.Repo, ExtensionInfo.Version, tagPrefix: ExtensionInfo.TagPrefix)
                .Section("General", "General", g => g
                    .Intro("`!refund @user [count]` refunds the latest pending channel-point redemptions for that user. " + "Count defaults to **1**. Chat gets a confirm window, then the timed action commits. " + "`!refundcancel` aborts. Broadcaster can always self-refund; mods cannot unless the toggle is on.")
                    .Toggle("Mod self refund", "mod_self_refund")
                        .Hint("If off, mods cannot !refund themselves. The broadcaster still can.")
                        .Default(false)
                    .Toggle("Print individual rewards", "print_rewards")
                        .Hint("Chat lists stacked reward names and costs. Off = count and total points only.")
                        .Default(true)
                    .Grid("grid-cols-2 items-center", row => row
                        .IntegerInput("Confirm delay (seconds)", "confirm_delay")
                            .Hint("How long chat has to !refundcancel before Twitch is called.")
                            .Range(3, 60)
                            .Default(10)
                            .Size("w-fit min-w-20")
                        .IntegerInput("Max refund count", "max_count")
                            .Hint("Caps !refund @user N so a typo cannot dump the whole queue.")
                            .Range(1, 100)
                            .Default(25)
                            .Size("w-fit min-w-20"))));
        return true;
    }

    private static void SeedDefaults(JObject o)
    {
        o["mod_self_refund"] = false;
        o["print_rewards"] = true;
        o["confirm_delay"] = 10;
        o["max_count"] = 25;
    }
}