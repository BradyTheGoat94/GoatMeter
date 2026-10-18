#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aion2DPSPro.Updates;
using Xunit;

public class UpdaterTests
{
    const string Root="https://github.com/BradyTheGoat94/GoatMeter/releases/download/";
    static readonly byte[] Installer=Encoding.UTF8.GetBytes("synthetic installer bytes - never executable");
    static object Release(int build,bool beta=false,bool draft=false,bool missing=false,string? badUrl=null)
    {
        var tag=(beta?"goatmeter-beta-":"goatmeter-build-")+build;
        var assets=new List<object>{new {name="GoatMeter-Setup.exe",browser_download_url=badUrl??Root+tag+"/GoatMeter-Setup.exe"}};
        if(!missing)assets.Add(new {name="SHA256SUMS.txt",browser_download_url=Root+tag+"/SHA256SUMS.txt"});
        return new {tag_name=tag,prerelease=beta,draft,assets};
    }
    sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> respond):HttpMessageHandler
    {
        public List<string> Requests=new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {Requests.Add(request.RequestUri!.ToString());return respond(request,token);}
    }
    static Handler Json(object value)=>new((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value))}));
    static GoatMeterUpdater.AvailableUpdate Update=>new(new Version(1,0,219),Root+"goatmeter-beta-219/GoatMeter-Setup.exe",Root+"goatmeter-beta-219/SHA256SUMS.txt",true);
    static string Folder()=>Path.Combine(Path.GetTempPath(),"GoatMeter-Updater-Tests",Guid.NewGuid().ToString("N"));
    static void Remove(string path){if(Directory.Exists(path))Directory.Delete(path,true);}

    [Fact] public async Task StableOnlyUsesLatestStableEndpoint()
    {
        using var handler=Json(Release(219));using var client=new HttpClient(handler);
        var update=await new UpdateService(client).CheckAsync(new(1,0,218),UpdateChannel.Stable);
        Assert.Equal(new Version(1,0,219),update!.Version);Assert.False(update.IsBeta);
        Assert.EndsWith("/releases/latest",Assert.Single(handler.Requests));
    }
    [Theory][InlineData(217)][InlineData(218)]
    public async Task NeverDowngradesOrReinstallsSameVersion(int build)
    {
        using var handler=Json(Release(build));using var client=new HttpClient(handler);
        Assert.Null(await new UpdateService(client).CheckAsync(new(1,0,218,0),UpdateChannel.Stable));
    }
    [Theory][InlineData(true,false,false)][InlineData(false,true,false)][InlineData(false,false,true)]
    public async Task StableRejectsBetaDraftAndIncompleteRelease(bool beta,bool draft,bool missing)
    {
        using var handler=Json(Release(219,beta,draft,missing));using var client=new HttpClient(handler);
        Assert.Null(await new UpdateService(client).CheckAsync(new(1,0,218),UpdateChannel.Stable));
    }
    [Fact] public async Task BetaSelectsNewestCompatibleReleaseRegardlessOfListOrder()
    {
        using var handler=Json(new[]{Release(220),Release(219,true),Release(999,true,true),Release(221,true),Release(222,true,missing:true)});
        using var client=new HttpClient(handler);
        var update=await new UpdateService(client).CheckAsync(new(1,0,218),UpdateChannel.Beta);
        Assert.Equal(new Version(1,0,221),update!.Version);Assert.True(update.IsBeta);
        Assert.Contains("per_page=100",Assert.Single(handler.Requests));
    }
    [Fact] public async Task BetaCanGraduateToNewerStableRelease()
    {
        using var handler=Json(new[]{Release(219),Release(218,true)});using var client=new HttpClient(handler);
        Assert.False((await new UpdateService(client).CheckAsync(new(1,0,218),UpdateChannel.Beta))!.IsBeta);
    }
    [Theory]
    [InlineData("https://example.com/GoatMeter-Setup.exe")]
    [InlineData(Root+"goatmeter-build-218/GoatMeter-Setup.exe")]
    [InlineData(Root+"goatmeter-build-219/GoatMeter-Setup.exe?other=1")]
    public async Task RejectsUnrelatedAndMismatchedReleaseAssets(string url)
    {
        using var handler=Json(Release(219,badUrl:url));using var client=new HttpClient(handler);
        Assert.Null(await new UpdateService(client).CheckAsync(new(1,0,218),UpdateChannel.Stable));
    }
    [Fact] public async Task MissingReleaseDoesNotInterruptStartup()
    {
        using var handler=new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));using var client=new HttpClient(handler);
        Assert.Null(await new UpdateService(client).CheckAsync(new(1,0,218),UpdateChannel.Stable));
    }
    [Fact] public async Task DownloadVerifiesBytesAndKeepsSeparateAttempts()
    {
        var hash=Convert.ToHexString(SHA256.HashData(Installer)).ToLowerInvariant();
        using var handler=new Handler((r,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=r.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt")?new StringContent("\uFEFF"+hash+"  GoatMeter-Setup.exe\r\n"):new ByteArrayContent(Installer)}));
        using var client=new HttpClient(handler);var folder=Folder();
        try
        {
            var service=new UpdateService(client);var first=await service.DownloadAsync(Update,folder);var second=await service.DownloadAsync(Update,folder);
            Assert.NotEqual(first,second);Assert.Equal(Installer,await File.ReadAllBytesAsync(first));
            Assert.Empty(Directory.GetFiles(folder,"*.partial",SearchOption.AllDirectories));
        }
        finally {Remove(folder);}
    }
    [Theory][InlineData("bad")][InlineData("missing")][InlineData("duplicate")][InlineData("mismatch")][InlineData("truncated")]
    public async Task InvalidChecksumAndFailedTransfersNeverLeaveInstallableFile(string fault)
    {
        var hash=Convert.ToHexString(SHA256.HashData(Installer));var line=hash+"  GoatMeter-Setup.exe\n";
        var sums=fault switch {"bad"=>"wrong  GoatMeter-Setup.exe","missing"=>"", "duplicate"=>line+line,"mismatch"=>new string('0',64)+"  GoatMeter-Setup.exe",_=>line};
        using var handler=new Handler((r,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=r.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt")?new StringContent(sums):new ByteArrayContent(fault=="truncated"?Installer[..3]:Installer)}));
        using var client=new HttpClient(handler);var folder=Folder();
        try {await Assert.ThrowsAsync<InvalidDataException>(()=>new UpdateService(client).DownloadAsync(Update,folder));Assert.True(!Directory.Exists(folder)||!Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).Any());}
        finally {Remove(folder);}
    }
    [Fact] public async Task CancelledTransferCleansPartialDownload()
    {
        using var handler=new Handler((r,_)=>r.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS.txt")?Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(new string('0',64)+"  GoatMeter-Setup.exe")}):Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true)));
        using var client=new HttpClient(handler);var folder=Folder();
        try {await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new UpdateService(client).DownloadAsync(Update,folder));Assert.True(!Directory.Exists(folder)||!Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).Any());}
        finally {Remove(folder);}
    }
    [Fact] public async Task DownloadRevalidatesPublicRequestObject()
    {
        using var handler=Json(new{});using var client=new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(()=>new UpdateService(client).DownloadAsync(Update with {InstallerUrl="https://example.com/setup.exe"},Folder()));
        Assert.Empty(handler.Requests);
    }
    [Fact] public void PackagedChannelMatchesAssemblyMetadata()
    {
        var value=typeof(GoatMeterUpdater).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(x=>x.Key=="UpdateChannel").Value;
        Assert.Equal(value=="Beta"?UpdateChannel.Beta:UpdateChannel.Stable,GoatMeterUpdater.Channel);
    }
}
