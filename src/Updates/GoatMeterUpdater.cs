using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aion2DPSPro.Updates;

/// <summary>Opt-in installer updates from the official stable GitHub release only.</summary>
public static class GoatMeterUpdater
{
    private const string Api = "https://api.github.com/repos/BradyTheGoat94/GoatMeter/releases/latest";
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("GoatMeter-Updater/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public sealed record AvailableUpdate(Version Version, string InstallerUrl, string ChecksumsUrl);

    public static async Task<AvailableUpdate?> CheckAsync(CancellationToken token = default)
    {
        using var response = await Client.GetAsync(Api, token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        // Stable releases are tagged goatmeter-build-N and the installer version
        // is 1.0.N. Never accept arbitrary tags or update from test branches.
        const string prefix = "goatmeter-build-";
        if (!tag.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(tag.AsSpan(prefix.Length), out var build) || build < 1) return null;
        var remote = new Version(1, 0, build);
        var local = typeof(GoatMeterUpdater).Assembly.GetName().Version ?? new Version(1, 0, 0);
        if (remote <= local) return null;

        string? installer = null, checksums = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString();
            if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                || parsed.Scheme != Uri.UriSchemeHttps || parsed.Host != "github.com"
                || !parsed.AbsolutePath.StartsWith("/BradyTheGoat94/GoatMeter/releases/download/", StringComparison.Ordinal))
                continue;
            if (name == "GoatMeter-Setup.exe") installer = url;
            if (name == "SHA256SUMS.txt") checksums = url;
        }
        return installer is not null && checksums is not null
            ? new AvailableUpdate(remote, installer, checksums) : null;
    }

    public static async Task<string> DownloadVerifiedInstallerAsync(AvailableUpdate update, CancellationToken token = default)
    {
        var manifest = await Client.GetStringAsync(update.ChecksumsUrl, token);
        var line = manifest.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.EndsWith("  GoatMeter-Setup.exe", StringComparison.Ordinal));
        if (line is null || line.Length != 64 + 2 + "GoatMeter-Setup.exe".Length) throw new InvalidDataException("Installer checksum missing.");
        var expected = line[..64];
        if (!expected.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid installer checksum.");
        var folder = Path.Combine(Path.GetTempPath(), "GoatMeter-Updates");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"GoatMeter-Setup-{update.Version}.exe");
        var partial = target + ".partial";
        try
        {
            using (var response = await Client.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(token);
                await using var output = File.Create(partial);
                await source.CopyToAsync(output, token);
            }
            await using (var file = File.OpenRead(partial))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Downloaded installer failed SHA-256 verification.");
            }
            File.Move(partial, target, true);
            return target;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    public static void LaunchInstaller(string verifiedInstaller)
    {
        if (!File.Exists(verifiedInstaller)) throw new FileNotFoundException("Installer missing.", verifiedInstaller);
        Process.Start(new ProcessStartInfo(verifiedInstaller) { UseShellExecute = true });
    }
}
