using FluentConfig;
using FluentConfig.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

// Refs: System, System.Core, Newtonsoft.Json.dll, FluentConfig.dll (Streamer.bot dlls/).
#if EXTERNAL_EDITOR
public class AlertsMain : CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Title = "Alerts";
    private const string Version = "1.3.3";
    private const string Repo = "ddev01/streamerbot-scripts";
    private const string Usage = "!alert <name>";
    public bool Execute()
    {
        return true;
    }

    public bool OnRedeem()
    {
        AlertsOverlayDisk.Ensure(Version, Repo);
        var log = Fc.Logger(CPH, Title, Version);
        CPH.TryGetArg("rewardId", out string rewardId);
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            log.Warn("OnRedeem skipped: no rewardId.");
            return true;
        }

        var settings = LoadSettings();
        var alert = settings.Alerts.FirstOrDefault(a => a.Enabled && Eq(a.RewardId, rewardId));
        if (alert == null)
            return true;
        var ev = Fc.CaptureEvent(CPH);
        PlayAlert(alert, ev, log);
        TryFulfill(settings, rewardId);
        return true;
    }

    public bool OnCommand()
    {
        AlertsOverlayDisk.Ensure(Version, Repo);
        var log = Fc.Logger(CPH, Title, Version);
        var settings = LoadSettings();
        var ev = Fc.CaptureEvent(CPH);
        if (!IsModOrBroadcaster(ev))
        {
            Reply("Mods only.", ev);
            return true;
        }

        string name = FirstArg(ev);
        if (string.IsNullOrWhiteSpace(name) || Eq(name, "list") || Eq(name, "help"))
        {
            var names = settings.Alerts.Where(a => a.Enabled).Select(a => a.Name).ToArray();
            Reply(names.Length == 0 ? "No alerts configured. Open Alerts settings." : "Alerts: " + string.Join(", ", names) + ". " + Usage, ev);
            return true;
        }

        var alert = FindAlert(settings, name);
        if (alert == null)
        {
            Reply("Unknown alert '" + name + "'.", ev);
            return true;
        }

        string missing = MissingMediaHint(alert);
        if (!string.IsNullOrWhiteSpace(missing))
            Reply(missing, ev);
        PlayAlert(alert, ev, log);
        return true;
    }

    public bool PlayNamed()
    {
        AlertsOverlayDisk.Ensure(Version, Repo);
        var log = Fc.Logger(CPH, Title, Version);
        var settings = LoadSettings();
        string name = "";
        if (CPH.TryGetArg("alert", out string a) && !string.IsNullOrWhiteSpace(a))
            name = a.Trim();
        else if (CPH.TryGetArg("alertName", out string b) && !string.IsNullOrWhiteSpace(b))
            name = b.Trim();
        else if (CPH.TryGetArg("rawInput", out string raw) && !string.IsNullOrWhiteSpace(raw))
            name = raw.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            log.Warn("PlayNamed skipped: pass alert / alertName / rawInput.");
            return false;
        }

        var alert = FindAlert(settings, name);
        if (alert == null)
        {
            log.Warn("PlayNamed: unknown alert '" + name + "'.");
            return false;
        }

        PlayAlert(alert, Fc.CaptureEvent(CPH), log);
        return true;
    }

    private void PlayAlert(AlertConfig alert, EventContext ev, ExtensionLogger log)
    {
        string gif = ResolveOverlayMedia(alert.GifPath, alert.Name, "GIF", log);
        string sound = ResolveOverlayMedia(alert.SoundPath, alert.Name, "sound", log);
        string user = Mention(ev);
        string text = "";
        if (!string.IsNullOrWhiteSpace(alert.Text))
        {
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
                ["alert"] = alert.Name,
            };
            text = Fc.ApplyTemplate(alert.Text, vars);
        }

        var payload = new JObject
        {
            ["type"] = "choppa.alert",
            ["gif"] = gif ?? "",
            ["sound"] = sound ?? "",
            ["text"] = text ?? "",
            ["volume"] = alert.Volume,
            ["durationMs"] = alert.DurationMs,
        };
        CPH.WebsocketBroadcastJson(payload.ToString(Formatting.None));
        log.Info("Played " + alert.Name + " gif=" + (string.IsNullOrEmpty(gif) ? "-" : "yes") + " sound=" + (string.IsNullOrEmpty(sound) ? "-" : "yes"));
    }

    private static string ResolveOverlayMedia(string path, string alertName, string kind, ExtensionLogger log)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        if (AlertsOverlayDisk.IsMissingLocalFile(path))
        {
            log.Warn("Alert '" + alertName + "' " + kind + " missing: " + path);
            return "";
        }

        return AlertsOverlayDisk.OverlaySrc(AlertsOverlayDisk.ImportMedia(path));
    }

    private static string MissingMediaHint(AlertConfig alert)
    {
        bool gifMiss = AlertsOverlayDisk.IsMissingLocalFile(alert.GifPath);
        bool soundMiss = AlertsOverlayDisk.IsMissingLocalFile(alert.SoundPath);
        if (!gifMiss && !soundMiss)
            return "";
        if (gifMiss && soundMiss)
            return "Alert '" + alert.Name + "' is missing its GIF and sound files. Re-pick them in Alerts settings.";
        if (gifMiss)
            return "Alert '" + alert.Name + "' is missing its GIF/video file. Re-pick it in Alerts settings.";
        return "Alert '" + alert.Name + "' is missing its sound file. Re-pick it in Alerts settings.";
    }

    private void TryFulfill(Settings settings, string rewardId)
    {
        if (!settings.FulfillAfterPlay)
            return;
        CPH.TryGetArg("redemptionId", out string redemptionId);
        if (string.IsNullOrWhiteSpace(rewardId) || string.IsNullOrWhiteSpace(redemptionId))
            return;
        try
        {
            CPH.TwitchRedemptionFulfill(rewardId, redemptionId);
        }
        catch
        {
        }
    }

    private Settings LoadSettings()
    {
        var settings = new Settings
        {
            FulfillAfterPlay = Fc.GetSetting(CPH, Title, "fulfill_after_play", true),
            WsHost = (Fc.GetSetting(CPH, Title, "ws_host", "127.0.0.1") ?? "127.0.0.1").Trim(),
            WsPort = Clamp(Fc.GetSetting(CPH, Title, "ws_port", 8080), 1, 65535),
        };
        string[] names = Fc.GetSetting(CPH, Title, "alerts", Array.Empty<string>());
        if (names == null)
            names = Array.Empty<string>();
        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            string name = raw.Trim();
            settings.Alerts.Add(new AlertConfig { Name = name, Enabled = Fc.GetSetting(CPH, Title, name + "_enabled", true), RewardId = Fc.GetSetting(CPH, Title, name + "_reward_id", "") ?? "", Command = (Fc.GetSetting(CPH, Title, name + "_command", "") ?? "").Trim().TrimStart('!'), GifPath = Fc.GetSetting(CPH, Title, name + "_gif", "") ?? "", SoundPath = Fc.GetSetting(CPH, Title, name + "_sound", "") ?? "", Text = Fc.GetSetting(CPH, Title, name + "_text", "") ?? "", Volume = Clamp(Fc.GetSetting(CPH, Title, name + "_volume", 100), 0, 100), DurationMs = Clamp(Fc.GetSetting(CPH, Title, name + "_duration_ms", 0), 0, 120000), });
        }

        return settings;
    }

    private static AlertConfig FindAlert(Settings settings, string name)
    {
        name = (name ?? "").Trim().TrimStart('!');
        return settings.Alerts.FirstOrDefault(a => a.Enabled && (Eq(a.Name, name) || Eq(a.Command, name)));
    }

    private static string FirstArg(EventContext ev)
    {
        string raw = ev.RawInput;
        if (string.IsNullOrWhiteSpace(raw))
            raw = ev.Message ?? "";
        raw = raw.Trim();
        if (raw.StartsWith("!alert", StringComparison.OrdinalIgnoreCase))
            raw = raw.Substring(6).Trim();
        if (raw.StartsWith("alert ", StringComparison.OrdinalIgnoreCase))
            raw = raw.Substring(6).Trim();
        int space = raw.IndexOf(' ');
        return space < 0 ? raw : raw.Substring(0, space);
    }

    private static string Mention(EventContext ev)
    {
        string n = !string.IsNullOrWhiteSpace(ev.User) ? ev.User : ev.UserName;
        return string.IsNullOrWhiteSpace(n) ? "user" : n.Trim();
    }

    private void Reply(string message, EventContext ev)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        if (!string.IsNullOrWhiteSpace(ev.MessageId))
            CPH.TwitchReplyToMessage(message, ev.MessageId);
        else
            CPH.SendMessage(message);
    }

    private bool IsModOrBroadcaster(EventContext ev)
    {
        if (ev.IsModerator)
            return true;
        if (CPH.TryGetArg("isBroadcaster", out bool b) && b)
            return true;
        try
        {
            var broadcaster = CPH.TwitchGetBroadcaster();
            if (broadcaster == null)
                return false;
            if (!string.IsNullOrWhiteSpace(ev.UserId) && string.Equals(broadcaster.UserId, ev.UserId, StringComparison.OrdinalIgnoreCase))
                return true;
            if (Eq(broadcaster.UserName, ev.User) || Eq(broadcaster.UserName, ev.UserName))
                return true;
        }
        catch
        {
        }

        return false;
    }

    private static bool Eq(string a, string b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    private sealed class Settings
    {
        public bool FulfillAfterPlay = true;
        public string WsHost = "127.0.0.1";
        public int WsPort = 8080;
        public List<AlertConfig> Alerts = new List<AlertConfig>();
    }

    private sealed class AlertConfig
    {
        public string Name;
        public bool Enabled;
        public string RewardId;
        public string Command;
        public string GifPath;
        public string SoundPath;
        public string Text;
        public int Volume;
        public int DurationMs;
    }
}

static class AlertsOverlayDisk
{
    public const string GitBranch = "develop";
    public const string GitPath = "my-scripts/alerts/";
    public const string BrandFolder = "Choppa";
    public const string ExtensionFolder = "Alerts";
    public static string SbRoot()
    {
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>Streamer.bot/Choppa. Extension data (not FluentConfig.dll, not the GitHub username).</summary>
    public static string BrandRoot()
    {
        return Path.Combine(SbRoot(), BrandFolder);
    }

    /// <summary>Streamer.bot/Choppa/Alerts. overlay.html, picker.html, streamerbot-client.js.</summary>
    public static string Root()
    {
        return Path.Combine(BrandRoot(), ExtensionFolder);
    }

    public static string HtmlPath()
    {
        return Path.Combine(Root(), "overlay.html");
    }

    public static string PickerPath()
    {
        return Path.Combine(Root(), "picker.html");
    }

    /// <summary>Streamer.bot/Choppa/media. Shared across Choppa extensions. Never wiped on update.</summary>
    public static string MediaDir()
    {
        return Path.Combine(BrandRoot(), "media");
    }

    public static string LegacyRoot()
    {
        return Path.Combine(SbRoot(), "overlays", "choppa-alerts");
    }

    public static string Ensure(string version, string repo)
    {
        Directory.CreateDirectory(BrandRoot());
        Directory.CreateDirectory(Root());
        Directory.CreateDirectory(MediaDir());
        MigrateLegacy();
        string stamp = Path.Combine(Root(), "overlay.version");
        bool stale = !File.Exists(HtmlPath()) || !File.Exists(PickerPath()) || !File.Exists(stamp) || !string.Equals((File.ReadAllText(stamp) ?? "").Trim(), version ?? "", StringComparison.Ordinal);
        if (stale)
        {
            bool overlayOk = TryPull(repo, "overlay.html", HtmlPath());
            bool pickerOk = TryPull(repo, "picker.html", PickerPath());
            if (overlayOk && pickerOk)
                File.WriteAllText(stamp, version ?? "", new UTF8Encoding(false));
        }

        EnsureClient(Root());
        return new Uri(HtmlPath()).AbsoluteUri;
    }

    public static void MigrateLegacy()
    {
        string oldRoot = LegacyRoot();
        if (!Directory.Exists(oldRoot))
            return;
        CopyFileIfMissing(Path.Combine(oldRoot, "overlay.html"), HtmlPath());
        CopyFileIfMissing(Path.Combine(oldRoot, "picker.html"), PickerPath());
        CopyFileIfMissing(Path.Combine(oldRoot, "streamerbot-client.js"), Path.Combine(Root(), "streamerbot-client.js"));
        string oldMedia = Path.Combine(oldRoot, "media");
        if (!Directory.Exists(oldMedia))
            return;
        foreach (string src in Directory.GetFiles(oldMedia))
            CopyFileIfMissing(src, Path.Combine(MediaDir(), Path.GetFileName(src)));
    }

    private static void CopyFileIfMissing(string src, string dest)
    {
        if (!File.Exists(src) || File.Exists(dest))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? BrandRoot());
        File.Copy(src, dest, false);
    }

    public static bool TryPull(string repo, string fileName, string dest)
    {
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(fileName))
            return false;
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string[] urls =
        {
            "https://raw.githubusercontent.com/" + repo + "/" + GitBranch + "/" + GitPath + fileName,
            "https://cdn.jsdelivr.net/gh/" + repo + "@" + GitBranch + "/" + GitPath + fileName,
        };
        string tmp = dest + ".tmp";
        foreach (string url in urls)
        {
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "ChoppaAlerts");
                    wc.DownloadFile(url, tmp);
                }

                if (!File.Exists(tmp) || new FileInfo(tmp).Length < 32)
                    continue;
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Move(tmp, dest);
                return true;
            }
            catch
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch
                {
                }
            }
        }

        return File.Exists(dest);
    }

    private static void EnsureClient(string root)
    {
        string path = Path.Combine(root, "streamerbot-client.js");
        if (File.Exists(path) && new FileInfo(path).Length > 1000)
            return;
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var wc = new WebClient())
                wc.DownloadFile("https://cdn.jsdelivr.net/npm/@streamerbot/client/dist/streamerbot-client.js", path);
        }
        catch
        {
        }
    }

    public static string ObsUrl(string host, int port, string password)
    {
        string url = new Uri(HtmlPath()).AbsoluteUri;
        string h = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        int p = port > 0 ? port : 8080;
        var q = new StringBuilder();
        q.Append("address=").Append(Uri.EscapeDataString(h));
        q.Append("&port=").Append(p.ToString());
        if (!string.IsNullOrWhiteSpace(password))
            q.Append("&password=").Append(Uri.EscapeDataString(password));
        return url + "?" + q;
    }

    public static bool IsRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        path = path.Trim();
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMissingLocalFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        path = path.Trim();
        if (IsRemotePath(path))
            return false;
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return false;
        return !File.Exists(path);
    }

    public static string ImportMedia(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return "";
        sourcePath = sourcePath.Trim();
        if (IsRemotePath(sourcePath) || sourcePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return sourcePath;
        if (!File.Exists(sourcePath))
            return sourcePath;
        string media = MediaDir();
        Directory.CreateDirectory(media);
        string full = Path.GetFullPath(sourcePath);
        string mediaFull = Path.GetFullPath(media);
        if (!mediaFull.EndsWith(Path.DirectorySeparatorChar.ToString()))
            mediaFull += Path.DirectorySeparatorChar;
        if (full.StartsWith(mediaFull, StringComparison.OrdinalIgnoreCase))
            return full;
        string dest = UniqueDest(media, Path.GetFileName(full));
        File.Copy(full, dest, false);
        return dest;
    }

    public static string OverlaySrc(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        path = path.Trim();
        if (IsRemotePath(path) || path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return path;
        if (!File.Exists(path))
            return "";
        try
        {
            string overlayDir = Path.GetFullPath(Root());
            if (!overlayDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                overlayDir += Path.DirectorySeparatorChar;
            var from = new Uri(overlayDir);
            var to = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(from.MakeRelativeUri(to).ToString()).Replace('\\', '/');
        }
        catch
        {
            try
            {
                return new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }
            catch
            {
                return path;
            }
        }
    }

    public static string UniqueDest(string dir, string fileName)
    {
        string safe = SafeFile(fileName);
        string dest = Path.Combine(dir, safe);
        if (!File.Exists(dest))
            return dest;
        string stem = Path.GetFileNameWithoutExtension(safe);
        string ext = Path.GetExtension(safe);
        for (int i = 2; i < 1000; i++)
        {
            dest = Path.Combine(dir, stem + "_" + i + ext);
            if (!File.Exists(dest))
                return dest;
        }

        return Path.Combine(dir, stem + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext);
    }

    public static string SafeFile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "media.bin";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (char c in name.Trim())
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        string s = sb.ToString().Trim('.', ' ');
        return string.IsNullOrWhiteSpace(s) ? "media.bin" : s;
    }
}