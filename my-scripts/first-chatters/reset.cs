// Refs: FluentConfig.dll (Streamer.bot dlls/) — Fc.KeyFor derives snake_case global names.
using FluentConfig;

#if EXTERNAL_EDITOR
public class FirstChattersReset : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "First Chatters";
    private const string Version = "1.0.0";
    private static readonly string CounterVar = Fc.KeyFor(Title, "counter");
    private static readonly string UsersVar = Fc.KeyFor(Title, "users");
    private static readonly string ActionIdVar = Fc.KeyFor(Title, "action_id");
    public bool Execute()
    {
        var log = Fc.Logger(CPH, Title, Version);
        CPH.SetGlobalVar(CounterVar, 0, true);
        CPH.SetGlobalVar(UsersVar, "[]", true);
        log.Info("Reset counter and winners.");
        string actionId = CPH.GetGlobalVar<string>(ActionIdVar, true);
        if (string.IsNullOrWhiteSpace(actionId))
        {
            log.Warn("Cannot re-enable main: no saved action id. Run the main action once from a trigger first.");
            return true;
        }

        CPH.EnableActionById(actionId);
        log.Info("Main action re-enabled.");
        return true;
    }
}