using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aion2DPSPro.Updates;

public enum UpdateChannel { Stable, Beta }

/// <summary>Installed builds opt into their own channel; stable users never receive betas.</summary>
public static class GoatMeterUpdater
{
    static readonly HttpClient Client = CreateClient();
    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GoatMeter-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
    public static UpdateChannel Channel => typeof(GoatMeterUpdater).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Any(x=>x.Key=="UpdateChannel"&&x.Value=="Beta")
            ? UpdateChannel.Beta : UpdateChannel.Stable;
    public sealed record AvailableUpdate(Version Version, string InstallerUrl, string ChecksumsUrl, bool IsBeta=false);
    public static Task<AvailableUpdate?> CheckAsync(CancellationToken token=default)
        => new UpdateService(Client).CheckAsync(typeof(GoatMeterUpdater).Assembly.GetName().Version??new Version(1,0,0),Channel,token);
    public static Task<string> DownloadVerifiedInstallerAsync(AvailableUpdate update,CancellationToken token=default)
        => new UpdateService(Client).DownloadAsync(update,Path.Combine(Path.GetTempPath(),"GoatMeter-Updates"),token);
    public static void LaunchInstaller(string verifiedInstaller)
    {
        if(!File.Exists(verifiedInstaller))throw new FileNotFoundException("Installer missing.",verifiedInstaller);
        // Inno Setup waits for the app's executable to be released before replacing files.
        Process.Start(new ProcessStartInfo(verifiedInstaller) { UseShellExecute=true });
    }
}

public sealed class UpdateService(HttpClient client)
{
    const string Repo="https://api.github.com/repos/BradyTheGoat94/GoatMeter";
    const string Download="https://github.com/BradyTheGoat94/GoatMeter/releases/download/";
    public async Task<GoatMeterUpdater.AvailableUpdate?> CheckAsync(Version current,UpdateChannel channel,CancellationToken token=default)
    {
        var url=channel==UpdateChannel.Beta?$"{Repo}/releases?per_page=100":$"{Repo}/releases/latest";
        using var response=await client.GetAsync(url,token);
        if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return null;
        response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var releases=channel==UpdateChannel.Beta?doc.RootElement.EnumerateArray().ToArray():new[]{doc.RootElement};
        return releases.Select(x=>ReadRelease(x,channel)).OfType<GoatMeterUpdater.AvailableUpdate>()
            .Where(x=>x.Version>current).OrderByDescending(x=>x.Version).ThenBy(x=>x.IsBeta).FirstOrDefault();
    }
    static GoatMeterUpdater.AvailableUpdate? ReadRelease(JsonElement release,UpdateChannel channel)
    {
        if(!release.TryGetProperty("draft",out var draft)||draft.ValueKind!=JsonValueKind.False)return null;
        if(!release.TryGetProperty("prerelease",out var pre)||pre.ValueKind is not (JsonValueKind.True or JsonValueKind.False))return null;
        bool beta=pre.GetBoolean();
        if(beta&&channel!=UpdateChannel.Beta)return null;
        if(!release.TryGetProperty("tag_name",out var tagField)||tagField.ValueKind!=JsonValueKind.String)return null;
        var tag=tagField.GetString()!;var prefix=beta?"goatmeter-beta-":"goatmeter-build-";
        if(!tag.StartsWith(prefix,StringComparison.Ordinal)||!int.TryParse(tag.AsSpan(prefix.Length),out var build)||build<1||tag!=prefix+build)return null;
        if(!release.TryGetProperty("assets",out var assets)||assets.ValueKind!=JsonValueKind.Array)return null;
        string? Asset(string name)
        {
            var matches=assets.EnumerateArray().Where(a=>a.ValueKind==JsonValueKind.Object&&a.TryGetProperty("name",out var n)&&n.ValueKind==JsonValueKind.String&&n.GetString()==name).ToArray();
            if(matches.Length!=1||!matches[0].TryGetProperty("browser_download_url",out var u)||u.ValueKind!=JsonValueKind.String)return null;
            var url=u.GetString();return url==Download+tag+"/"+name?url:null;
        }
        var installer=Asset("GoatMeter-Setup.exe");var sums=Asset("SHA256SUMS.txt");
        return installer!=null&&sums!=null?new(new Version(1,0,build),installer,sums,beta):null;
    }
    public async Task<string> DownloadAsync(GoatMeterUpdater.AvailableUpdate update,string folder,CancellationToken token=default)
    {
        var prefix=Download+(update.IsBeta?"goatmeter-beta-":"goatmeter-build-")+update.Version.Build+"/";
        if(update.Version.Major!=1||update.Version.Minor!=0||update.Version.Build<1||update.InstallerUrl!=prefix+"GoatMeter-Setup.exe"||update.ChecksumsUrl!=prefix+"SHA256SUMS.txt")
            throw new InvalidDataException("Update download location is not an official release.");
        var manifest=await client.GetStringAsync(update.ChecksumsUrl,token);
        var entries=manifest.TrimStart('\uFEFF').Split('\n').Select(x=>x.Trim()).Where(x=>x.EndsWith("  GoatMeter-Setup.exe",StringComparison.Ordinal)).ToArray();
        if(entries.Length!=1||entries[0].Length!=64+"  GoatMeter-Setup.exe".Length||!entries[0][..64].All(Uri.IsHexDigit))throw new InvalidDataException("Installer checksum missing or invalid.");
        var expected=entries[0][..64];
        // Separate attempts cannot overwrite a download awaiting installation.
        var attempt=Path.Combine(folder,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(attempt);
        var target=Path.Combine(attempt,$"GoatMeter-Setup-{update.Version}.exe");var partial=target+".partial";
        try
        {
            using(var response=await client.GetAsync(update.InstallerUrl,HttpCompletionOption.ResponseHeadersRead,token))
            {
                response.EnsureSuccessStatusCode();
                await using var source=await response.Content.ReadAsStreamAsync(token);
                await using var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                await source.CopyToAsync(output,token);
            }
            await using(var file=File.OpenRead(partial))
            {
                var actual=Convert.ToHexString(await SHA256.HashDataAsync(file,token));
                if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Downloaded installer failed SHA-256 verification.");
            }
            File.Move(partial,target);return target;
        }
        finally
        {
            if(File.Exists(partial))File.Delete(partial);
            if(!File.Exists(target))Directory.Delete(attempt);
        }
    }
}
