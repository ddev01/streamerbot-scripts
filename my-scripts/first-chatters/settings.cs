using FluentConfig;

// Refs: PresentationFramework, PresentationCore, WindowsBase,
//       FluentConfig.dll (Streamer.bot dlls/).
#if EXTERNAL_EDITOR
public class FirstChattersSettings : CPHInlineBase
#else
public class CPHInline
#endif
{
    private static class ExtensionInfo
    {
        public const string Title = "First Chatters";
        public const string Version = "1.0.0";
        public const string Repo = "ddev01/streamerbot-scripts";
        public const string TagPrefix = "first-chatters";
    }

    private static string ToOrdinal(int position)
    {
        int mod100 = position % 100;
        int mod10 = position % 10;
        string suffix = "th";
        if (mod100 < 11 || mod100 > 13)
        {
            if (mod10 == 1)
                suffix = "st";
            else if (mod10 == 2)
                suffix = "nd";
            else if (mod10 == 3)
                suffix = "rd";
        }

        return position + suffix;
    }

    private static int DefaultPlacePoints(int position) => 3000 / position;

    public bool Execute()
    {
        CPH.LogInfo($"[First Chatters] Opening settings ({ExtensionInfo.Title} v{ExtensionInfo.Version}).");
        Fc.Open(CPH, ExtensionInfo.Title, ExtensionInfo.Version, ui => ui
            .WithExtensionUpdateNotice(
                ExtensionInfo.Repo,
                ExtensionInfo.Version,
                tagPrefix: ExtensionInfo.TagPrefix)
            .Section("General", "General", s => s
                .Intro(
                    "Award the first chatters of each stream. " +
                    "Use a **Twitch Chat Message** trigger for the main action and **Twitch Stream Online** for reset. " +
                    "When all spots are claimed, main disables itself; reset re-enables it.")
                .IntegerInput("How many first chatters", "max_count")
                    .Hint("Number of people who can claim a spot (1 = only 1st, 3 = 1st/2nd/3rd, ...).")
                    .Range(1, 10)
                    .Default(3)
                    .Size("w-fit min-w-16")
                .Grid("grid-cols-2 items-center", g => g
                    .Toggle("Exclude broadcaster", "exclude_broadcaster")
                        .Hint("Ignore the broadcaster's own chat messages.")
                        .Default(true)
                    .Toggle("Exclude bots", "exclude_bot")
                        .Hint("Ignore known chat bots and the connected Twitch bot account.")
                        .Default(true)))
            .Section("Message", "Message", m => m
                .Intro(
                    "Message sent when someone claims a spot. Placeholders:\n\n" +
                    "- `{user}` - username\n" +
                    "- `{ordinal}` - place with suffix (`1st`, `2nd`, `3rd`, ...)\n" +
                    "- `{position}` - numeric place (`1`, `2`, `3`, ...)\n" +
                    "- `{points}` - points awarded (0 if points are off)")
                .Toggle("Send chat message", "send_message")
                    .Hint("When off, no chat message is sent (arguments are still set for chained actions).")
                    .Default(true)
                .Textbox("Chat message", "message")
                    .Multiline()
                    .Hint("Shown when a spot is claimed.")
                    .Default("Congrats @{user}, you're {ordinal}!")
                    .ShowWhen("send_message"))
            .Section("Points", "Points", p => p
                .Intro("Points per place. Extra fields appear live as you raise how many first chatters you allow.")
                .Toggle("Award points", "award_points")
                    .Hint("Add points to a Twitch user variable when someone claims a spot.")
                    .Default(true)
                .WithVisibility("award_points", inner => inner
                    .Textbox("Points variable", "points_variable")
                        .Hint("Twitch user variable name for the point balance (e.g. points).")
                        .Default("points")
                        .Size("w-1/2")
                    .Grid("grid-cols-3 items-center", g => g
                        .RepeatFor("max_count", (row, i) => row
                            .IntegerInput($"{ToOrdinal(i)} place points", $"points_{i}")
                                .Range(0, 10000000)
                                .Default(DefaultPlacePoints(i))
                                .Size("w-fit"))))));
        return true;
    }
}