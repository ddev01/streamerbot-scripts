using System;

public class CPHInline
{
    public bool Execute()
    {
        // Reset the first chatters tracking (non-persistent since it's session-based)
        CPH.SetGlobalVar("chatCounter", 0, false);
        CPH.SetGlobalVar("first", "", false);
        CPH.SetGlobalVar("second", "", false);
        CPH.SetGlobalVar("third", "", false);

        // Clear OBS text sources
        CPH.ObsSetGdiText("first_chatters", "first_chatter", "#1");
        CPH.ObsSetGdiText("first_chatters", "second_chatter", "#2");
        CPH.ObsSetGdiText("first_chatters", "third_chatter", "#3");
        CPH.ObsSetSourceVisibility("first_chatters", "confetti", false);
        CPH.ObsSetSourceVisibility("Fortnite", "first_chatters", true);

        // Enable the action by ID (more reliable than name)
        CPH.EnableActionById("de46f6ec-0a00-4760-a9fe-ae2c671f3f6f");

        return true;
    }
}
