using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Trigger: Twitch > General > Present Viewers. Stores [{Id, UserName}] in the
// non-persisted global "currentViewers", which fun-text-commands reads.
public class CPHInline
{
    public bool Execute()
    {
        if (!CPH.TryGetArg("users", out object users) || users == null)
        {
            CPH.LogWarn("[Present Viewers] No 'users' argument; is this action triggered by Present Viewers?");
            return false;
        }

        // 'users' is a collection of {id, userName, display, role, isSubscribed}; shape-agnostic parse.
        var token = JToken.FromObject(users);
        var items = token is JObject obj ? obj.Properties().Select(p => p.Value) : token.Children();
        var list = items
            .Select(u => new { Id = (string)u["id"], UserName = (string)u["userName"] })
            .Where(v => !string.IsNullOrEmpty(v.UserName))
            .ToList();

        CPH.SetGlobalVar("currentViewers", JsonConvert.SerializeObject(list), false);
        CPH.LogInfo($"[Present Viewers] Stored {list.Count} viewers.");
        return true;
    }
}
