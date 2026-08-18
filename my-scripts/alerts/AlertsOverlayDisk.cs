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