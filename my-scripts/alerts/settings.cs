using FluentConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

// Refs: PresentationFramework, PresentationCore, WindowsBase, System.Net.Http,
//       Microsoft.Web.WebView2.Wpf, Microsoft.Web.WebView2.Core, Newtonsoft.Json.dll,
//       FluentConfig.dll (Streamer.bot dlls/).
// Execute C# Method + Run on UI thread.
#if EXTERNAL_EDITOR
public class AlertsSettings : CPHInlineBase
#else
public class CPHInline
#endif
{
    private static class ExtensionInfo
    {
        public const string Title = "Alerts";
        public const string Version = "1.3.3";
        public const string Repo = "ddev01/streamerbot-scripts";
        public const string TagPrefix = "alerts";
    }

    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
    private const string SoundLibraryHint = "No in-app sound search like Giphy. Meme boards have no official API we can ship, and downloading from them in the menu would be scraping. Legal APIs (stock/CC libraries) are not meme soundboards. Open MyInstants in your browser, download an MP3, then File — we copy it into Chopa/media.";
    public bool Execute()
    {
        CPH.LogInfo($"[Alerts] Opening settings ({ExtensionInfo.Title} v{ExtensionInfo.Version}).");
        AlertsOverlayDisk.Ensure(ExtensionInfo.Version, ExtensionInfo.Repo);
        if (!Fc.HasSavedSettings(CPH, ExtensionInfo.Title))
            Fc.SaveSettings(CPH, ExtensionInfo.Title, SeedDefaults);
        Fc.SetSetting(CPH, ExtensionInfo.Title, "overlay_folder", AlertsOverlayDisk.Root());
        Fc.SetSetting(CPH, ExtensionInfo.Title, "media_folder", AlertsOverlayDisk.MediaDir());
        string missing = MissingMediaSummary();
        string overlayIntro = "Enable Streamer.bot **Servers/Clients -> WebSocket Server** (auto-start, 127.0.0.1:8080). " + "This menu installs files under **Choppa/Alerts** (player) and **Choppa/media** (shared GIFs/sounds). " + "Add **one** OBS Browser Source as a **local file** pointing at overlay.html " + "(shutdown when not visible **off**, refresh on scene **off**, Control audio via OBS **on**). " + "After an extension update, reopen this menu, then refresh the Browser Source if the player looks old.";
        if (!string.IsNullOrWhiteSpace(missing))
            overlayIntro += " **Missing files:** " + missing + " Pick them again (Local copies into Choppa/media).";
        string host = Fc.GetSetting(CPH, ExtensionInfo.Title, "ws_host", "127.0.0.1");
        int port = Fc.GetSetting(CPH, ExtensionInfo.Title, "ws_port", 8080);
        string password = Fc.GetSetting(CPH, ExtensionInfo.Title, "ws_password", "") ?? "";
        Fc.SetSetting(CPH, ExtensionInfo.Title, "obs_url", AlertsOverlayDisk.ObsUrl(host, port, password));
        Fc.Open(CPH, ExtensionInfo.Title, ExtensionInfo.Version, ui => ui
                .WithExtensionUpdateNotice(ExtensionInfo.Repo, ExtensionInfo.Version, tagPrefix: ExtensionInfo.TagPrefix)
                .Section("OBS overlay", "Overlay", o => o
                    .Intro(overlayIntro)
                    .Textbox("Overlay folder", "overlay_folder")
                        .Hint("Choppa/Alerts. OBS local file = overlay.html here.")
                    .Textbox("Media folder", "media_folder")
                        .Hint("Choppa/media. Shared. Giphy and Local copy files here so Downloads can be emptied later.")
                    .Textbox("OBS Browser Source URL", "obs_url")
                        .Hint("Paste this into OBS. Query string sets WebSocket host/port.")
                    .Grid("grid-cols-2 items-center", g => g
                        .Textbox("WebSocket host", "ws_host")
                            .Default("127.0.0.1")
                        .IntegerInput("WebSocket port", "ws_port")
                            .Range(1, 65535)
                            .Default(8080)
                            .Size("w-fit min-w-20"))
                    .Textbox("WebSocket password", "ws_password")
                        .Hint("Only if you set one in Streamer.bot. Stored in the OBS URL.")
                        .Password()
                    .Toggle("Fulfill reward after play", "fulfill_after_play")
                        .Hint("Marks the redemption complete. Off if you fulfill elsewhere.")
                        .Default(true)
                    .Textbox("Giphy API key", "giphy_key")
                        .Hint("Optional. Web key from the Giphy developer dashboard (not iOS/Android SDK). Used by Giphy on each alert.")
                        .Password()
                    .Row(r => r
                        .Button("Copy OBS URL")
                            .OnClick(CopyObsUrl)
                        .Button("Test overlay")
                            .OnClick(TestOverlay)))
                .Section("Alerts", "Alerts", a => a
                    .Intro("Create an alert, enable it, bind a Streamer.bot-owned Twitch reward (Platforms -> Twitch -> Channel Point Rewards). " + "GIF: Giphy or Local (copies into Choppa/media). Command alias is optional - mods can `!alert Name` without spending points.")
                    .PillInput("Alerts", "alerts")
                        .Hint("Name, then Enter. Example: Rickroll")
                        .ItemTemplate(item => item
                            .Title("Alert: {name}")
                            .Toggle("Enabled", "{name}_enabled")
                                .Default(true)
                            .Dropdown("Twitch reward", "{name}_reward_display")
                                .Hint("Refresh loads rewards from Twitch.")
                                .Searchable()
                                .AllowCustom()
                                .WithPairValue("{name}_reward_id")
                                .Refresh(ListRewardPairs)
                            .Textbox("Command alias", "{name}_command")
                                .Hint("Optional. Testing / mods: `!alert Name` or this alias, no channel points. Other actions can call PlayNamed.")
                            .Row("items-end gap-2", media => media
                                .Filepath("GIF / video", "{name}_gif")
                                .HideBrowse()
                                .Accept("gif", "webp", "webm", "mp4", "mov")
                                    .Size("grow")
                                .Button("Giphy")
                                    .Size("shrink-0")
                                    .OnClick(BrowseGiphy)
                                .Button("Local")
                                    .Size("shrink-0")
                                    .OnClick(BrowseLocalGif))
                            .Row("items-end gap-2", audio => audio
                                .Filepath("Sound", "{name}_sound")
                                .HideBrowse()
                                .Accept("mp3", "wav", "ogg", "m4a")
                                    .Size("grow")
                                .Button("File")
                                    .Size("shrink-0")
                                    .OnClick(BrowseLocalSound)
                                .Button("MyInstants")
                                    .Hint("Opens myinstants.com in your browser. Download an MP3 there, then File — we copy it into Chopa/media.")
                                    .Size("shrink-0")
                                    .OnClick(OpenMyInstants)
                                .Button("i")
                                    .Hint(SoundLibraryHint)
                                    .Size("shrink-0")
                                    .Color("#6b7280")
                                    .OnClick(ExplainSoundLibrary))
                            .Textbox("On-screen text", "{name}_text")
                                .Hint("Optional. `{user}` `{alert}`.")
                            .Grid("grid-cols-2 items-center", row => row
                                .Slider("Volume", "{name}_volume")
                                    .Range(0, 100)
                                    .Default(100)
                                .IntegerInput("Duration (ms)", "{name}_duration_ms")
                                    .Hint("0 = until media ends (cap 15s for GIFs).")
                                    .Range(0, 120000)
                                    .Default(0)
                                    .Size("w-fit min-w-24")))
                        .OnPillRemoved((name, ctx) =>
        {
            ctx.RemoveSettingsKeys(name + "_enabled", name + "_reward_display", name + "_reward_id", name + "_command", name + "_gif", name + "_sound", name + "_text", name + "_volume", name + "_duration_ms");
        })));
        return true;
    }

    private void CopyObsUrl(UiContext ui)
    {
        AlertsOverlayDisk.Ensure(ExtensionInfo.Version, ExtensionInfo.Repo);
        string url = AlertsOverlayDisk.ObsUrl(ui.Pending<string>("ws_host"), ui.Pending<int>("ws_port"), ui.Pending<string>("ws_password"));
        try
        {
            Clipboard.SetText(url);
            Fc.SetSetting(CPH, ExtensionInfo.Title, "obs_url", url);
            ui.Toast("Copied OBS URL.");
        }
        catch (Exception ex)
        {
            ui.Popup("OBS URL", url + "\n\n(Could not copy: " + ex.Message + ")");
        }
    }

    private void TestOverlay(UiContext ui)
    {
        AlertsOverlayDisk.Ensure(ExtensionInfo.Version, ExtensionInfo.Repo);
        string name = FirstEnabled(ui);
        string gif = "";
        string sound = "";
        string text = "Alerts overlay OK";
        int volume = 100;
        int duration = 4000;
        if (!string.IsNullOrWhiteSpace(name))
        {
            gif = AlertsOverlayDisk.OverlaySrc(AlertsOverlayDisk.ImportMedia(ui.Pending<string>(name + "_gif")));
            sound = AlertsOverlayDisk.OverlaySrc(AlertsOverlayDisk.ImportMedia(ui.Pending<string>(name + "_sound")));
            text = ui.Pending<string>(name + "_text");
            if (string.IsNullOrWhiteSpace(text))
                text = name;
            volume = ui.Pending<int>(name + "_volume");
            if (volume <= 0)
                volume = 100;
            duration = ui.Pending<int>(name + "_duration_ms");
            if (duration <= 0)
                duration = 5000;
        }

        var payload = new JObject
        {
            ["type"] = "choppa.alert",
            ["gif"] = gif ?? "",
            ["sound"] = sound ?? "",
            ["text"] = text ?? "",
            ["volume"] = volume,
            ["durationMs"] = duration,
        };
        CPH.WebsocketBroadcastJson(payload.ToString(Formatting.None));
        ui.Toast("Sent test play. If OBS stays empty: WebSocket Server must be running, then right-click the Browser Source ? Refresh.");
    }

    private static string AlertName(UiContext ui)
    {
        return (ui.ItemName ?? "").Trim();
    }

    private void BrowseGiphy(UiContext ui)
    {
        string apply = AlertName(ui);
        if (string.IsNullOrWhiteSpace(apply))
        {
            ui.Toast("Open an alert, then pick Giphy on that alert.");
            return;
        }

        string key = (ui.Pending<string>("giphy_key") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ui.Toast("Paste a Giphy Web API key on the Overlay tab first.");
            return;
        }

        AlertsOverlayDisk.Ensure(ExtensionInfo.Version, ExtensionInfo.Repo);
        if (!File.Exists(AlertsOverlayDisk.PickerPath()))
        {
            ui.Toast("Giphy picker did not download. Stay online and reopen settings.");
            return;
        }

        string dest = OpenGifPicker(key);
        if (string.IsNullOrWhiteSpace(dest))
            return;
        ui.SetPending(apply + "_gif", dest);
        ui.Toast("GIF saved for " + apply + ".");
    }

    private void BrowseLocalGif(UiContext ui)
    {
        string apply = AlertName(ui);
        if (string.IsNullOrWhiteSpace(apply))
        {
            ui.Toast("Open an alert, then pick Local on that alert.");
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "GIF / video",
            Filter = "GIF / video|*.gif;*.webp;*.webm;*.mp4;*.mov|All files|*.*",
        };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.FileName))
            return;
        ui.SetPending(apply + "_gif", AlertsOverlayDisk.ImportMedia(dlg.FileName));
    }

    private void BrowseLocalSound(UiContext ui)
    {
        string apply = AlertName(ui);
        if (string.IsNullOrWhiteSpace(apply))
        {
            ui.Toast("Open an alert, then pick File on that alert.");
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Sound",
            Filter = "Audio|*.mp3;*.wav;*.ogg;*.m4a|All files|*.*",
        };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.FileName))
            return;
        ui.SetPending(apply + "_sound", AlertsOverlayDisk.ImportMedia(dlg.FileName));
    }

    private static void OpenMyInstants(UiContext ui)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.myinstants.com/") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ui.Popup("MyInstants", "https://www.myinstants.com/\n\n(Could not open the browser: " + ex.Message + ")");
        }
    }

    private static void ExplainSoundLibrary(UiContext ui)
    {
        ui.Popup("Why no sound library", SoundLibraryHint);
    }

    private string OpenGifPicker(string apiKey)
    {
        string result = null;
        var win = new Window
        {
            Title = "Giphy",
            Width = 920,
            Height = 760,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        var wv = new WebView2();
        win.Content = wv;
        win.Loaded += async (s, e) =>
        {
            try
            {
                await wv.EnsureCoreWebView2Async();
                wv.CoreWebView2.SetVirtualHostNameToFolderMapping("choppa.alerts", AlertsOverlayDisk.Root(), CoreWebView2HostResourceAccessKind.Allow);
                wv.CoreWebView2.WebMessageReceived += (s2, ev) =>
                {
                    try
                    {
                        var o = JObject.Parse(ev.TryGetWebMessageAsString() ?? "{}");
                        if ((o["type"]?.ToString() ?? "") != "giphy")
                            return;
                        string url = o["url"]?.ToString();
                        string id = o["id"]?.ToString() ?? "gif";
                        string title = o["title"]?.ToString() ?? id;
                        if (string.IsNullOrWhiteSpace(url))
                            return;
                        result = DownloadGifToMedia(url, id, title);
                        win.Dispatcher.Invoke(() => win.Close());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Giphy");
                    }
                };
                wv.CoreWebView2.Navigate("https://choppa.alerts/picker.html?key=" + Uri.EscapeDataString(apiKey));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Giphy");
                win.Close();
            }
        };
        win.ShowDialog();
        try
        {
            wv.Dispose();
        }
        catch
        {
        }

        return result;
    }

    private string DownloadGifToMedia(string url, string id, string title)
    {
        Directory.CreateDirectory(AlertsOverlayDisk.MediaDir());
        string file = AlertsOverlayDisk.SafeFile((string.IsNullOrWhiteSpace(title) ? id : title) + ".gif");
        if (file.IndexOf('.') < 0)
            file += ".gif";
        string dest = AlertsOverlayDisk.UniqueDest(AlertsOverlayDisk.MediaDir(), file);
        byte[] bytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        File.WriteAllBytes(dest, bytes);
        return dest;
    }

    private static string FirstEnabled(UiContext ui)
    {
        var names = ui.Pending<string[]>("alerts") ?? Array.Empty<string>();
        foreach (var n in names)
        {
            if (string.IsNullOrWhiteSpace(n))
                continue;
            if (ui.Pending<bool>(n.Trim() + "_enabled"))
                return n.Trim();
        }

        return names.Select(x => (x ?? "").Trim()).FirstOrDefault(x => x.Length > 0) ?? "";
    }

    private (string value, string label)[] ListRewardPairs()
    {
        try
        {
            var rewards = CPH.TwitchGetRewards();
            if (rewards == null || rewards.Count == 0)
                return Array.Empty<(string, string)>();
            return rewards.Where(r => r != null && !string.IsNullOrWhiteSpace(r.Id)).Select(r => (r.Id, string.IsNullOrWhiteSpace(r.Title) ? r.Id : r.Title)).ToArray();
        }
        catch
        {
            return Array.Empty<(string, string)>();
        }
    }

    private string MissingMediaSummary()
    {
        var names = Fc.GetSetting(CPH, ExtensionInfo.Title, "alerts", Array.Empty<string>());
        if (names == null || names.Length == 0)
            return "";
        var sb = new StringBuilder();
        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            string name = raw.Trim();
            string gif = Fc.GetSetting(CPH, ExtensionInfo.Title, name + "_gif", "") ?? "";
            string sound = Fc.GetSetting(CPH, ExtensionInfo.Title, name + "_sound", "") ?? "";
            bool gifMiss = AlertsOverlayDisk.IsMissingLocalFile(gif);
            bool soundMiss = AlertsOverlayDisk.IsMissingLocalFile(sound);
            if (!gifMiss && !soundMiss)
                continue;
            if (sb.Length > 0)
                sb.Append("; ");
            sb.Append(name);
            if (gifMiss && soundMiss)
                sb.Append(" (GIF and sound)");
            else if (gifMiss)
                sb.Append(" (GIF)");
            else
                sb.Append(" (sound)");
        }

        return sb.ToString();
    }

    private static void SeedDefaults(JObject o)
    {
        o["ws_host"] = "127.0.0.1";
        o["ws_port"] = 8080;
        o["fulfill_after_play"] = true;
        o["alerts"] = new JArray();
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