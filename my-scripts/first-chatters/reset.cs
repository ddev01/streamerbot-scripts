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

        // Enable the action by ID (more reliable than name)
        CPH.EnableActionById("de46f6ec-0a00-4760-a9fe-ae2c671f3f6f");

        return true;
    }
}
