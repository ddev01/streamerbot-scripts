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
    private const string MainActionId = "f8df8367-7f50-48fe-a625-9221430d24d0";
    private static readonly string CounterVar = Fc.KeyFor(Title, "counter");
    private static readonly string UsersVar = Fc.KeyFor(Title, "users");
    public bool Execute()
    {
        var log = Fc.Logger(CPH, Title, Version);
        CPH.SetGlobalVar(CounterVar, 0, true);
        CPH.SetGlobalVar(UsersVar, "[]", true);
        log.Info("Reset counter and winners.");
        CPH.EnableActionById(MainActionId);
        log.Info("Main action re-enabled.");
        return true;
    }
}