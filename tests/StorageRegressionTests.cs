using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion2DPSPro.Storage;
using Xunit;

namespace Aion2DPSPro.Tests;

public sealed class StorageRegressionTests
{
    [Theory]
    [InlineData("Players")]
    [InlineData("Skills")]
    [InlineData("Buffs")]
    [InlineData("RecentEvents")]
    [InlineData("Targets")]
    [InlineData("Categories")]
    public async Task NullHistoryCollectionsAreRejectedAndNextLoadCanRecover(string property)
    {
        var root=Path.Combine(Path.GetTempPath(),"GoatMeter-storage-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store=new FightStore(root);
            var path=Path.Combine(root,"history.json");
            var valid=JsonSerializer.Serialize(Snapshot());
            var malformed=JsonNode.Parse(valid)!.AsObject();
            malformed[property]=null;
            await File.WriteAllTextAsync(path,malformed.ToJsonString());
            await Assert.ThrowsAsync<InvalidDataException>(()=>store.LoadAsync(path));
            await File.WriteAllTextAsync(path,valid);
            Assert.Equal(123,(await store.LoadAsync(path))!.FightDamage);
        }
        finally {Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData("player")]
    [InlineData("class")]
    [InlineData("flags")]
    [InlineData("category")]
    [InlineData("category-players")]
    [InlineData("duration")]
    public async Task MalformedHistoryRowsAndDurationFailBeforeReachingUi(string defect)
    {
        var root=Path.Combine(Path.GetTempPath(),"GoatMeter-storage-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path=Path.Combine(root,"history.json");
            var malformed=JsonNode.Parse(JsonSerializer.Serialize(Snapshot()))!.AsObject();
            switch(defect)
            {
                case "player": malformed["Players"]![0]=null; break;
                case "class": malformed["Players"]![0]!["ClassName"]=null; break;
                case "flags": malformed["Skills"]![0]!["FlagHits"]=null; break;
                case "category": malformed["Categories"]!["Damage"]=null; break;
                case "category-players": malformed["Categories"]!["Damage"]=JsonNode.Parse("{\"Players\":null,\"Skills\":[],\"MetricLabel\":\"DPS\"}"); break;
                case "duration": malformed["FightSeconds"]=double.MaxValue; break;
            }
            await File.WriteAllTextAsync(path,malformed.ToJsonString());
            await Assert.ThrowsAsync<InvalidDataException>(()=>new FightStore(root).LoadAsync(path));
        }
        finally {Directory.Delete(root,true);}
    }

    private static MeterSnapshot Snapshot()
    {
        var engine=new CombatEngine(()=>DateTime.UnixEpoch);
        engine.Apply(new(DateTime.UnixEpoch,CombatKind.PlayerName,7,"Hero"));
        engine.Apply(new(DateTime.UnixEpoch,CombatKind.Damage,7,"Hero",99,"Boss","Strike",123));
        return engine.Snapshot();
    }

    [Fact]
    public async Task MissingHistoryDirectoryIsCreatedOnFirstSave()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoatMeter-storage-"+Guid.NewGuid().ToString("N"));
        try
        {
            var folder=Path.Combine(root,"history");
            var store=new FightStore(folder);
            Assert.False(Directory.Exists(folder));
            Assert.Empty(store.List());
            var snapshot=Snapshot();
            await store.SaveAsync(snapshot);
            var loaded=await store.LoadAsync(Assert.Single(store.List()));
            Assert.Equal(snapshot.EncounterId,loaded!.EncounterId);
            Assert.Equal(123,loaded.FightDamage);
            Assert.Empty(Directory.EnumerateFiles(folder,"*.tmp"));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task UnavailableHistoryDoesNotBlockConstructionAndCanRecover()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoatMeter-storage-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var folder=Path.Combine(root,"history");
            await File.WriteAllTextAsync(folder,"A file blocks the history directory");
            var store=new FightStore(folder);
            await Assert.ThrowsAnyAsync<IOException>(()=>store.SaveAsync(Snapshot()));
            File.Delete(folder);
            await store.SaveAsync(Snapshot());
            Assert.Equal(123,(await store.LoadAsync(Assert.Single(store.List())))!.FightDamage);
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public async Task ConcurrentSavesOfOneEncounterKeepCompleteHistoryAndLeaveNoTemporaryFiles()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoatMeter-storage-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new FightStore(root);
            var snapshot=Snapshot();
            await Task.WhenAll(Enumerable.Range(0,32).Select(_=>store.SaveAsync(snapshot)));
            var loaded=await store.LoadAsync(Assert.Single(store.List()));
            Assert.Equal(snapshot.EncounterId,loaded!.EncounterId);
            Assert.Equal(123,loaded.FightDamage);
            Assert.Empty(Directory.EnumerateFiles(root,"*.tmp"));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task HistoryReadersAndAtomicReplacementsCanRunTogether()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoatMeter-storage-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new FightStore(root);
            var snapshot=Snapshot();
            await store.SaveAsync(snapshot);
            var path=Assert.Single(store.List());
            var tasks=Enumerable.Range(0,32).Select(async i=>
            {
                if(i%2==0) await store.SaveAsync(snapshot);
                else
                {
                    var loaded=await store.LoadAsync(path);
                    Assert.Equal(snapshot.EncounterId,loaded!.EncounterId);
                    Assert.Equal(123,loaded.FightDamage);
                }
            });
            await Task.WhenAll(tasks);
            Assert.Empty(Directory.EnumerateFiles(root,"*.tmp"));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public async Task FailedSavePreservesPreviousHistory()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoatMeter-storage-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new FightStore(root);
            var snapshot=Snapshot();
            await store.SaveAsync(snapshot);
            await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(snapshot with {FightDps=double.NaN}));
            var loaded=await store.LoadAsync(Assert.Single(store.List()));
            Assert.Equal(snapshot.FightDps,loaded!.FightDps);
            Assert.Equal(123,loaded.FightDamage);
            Assert.Empty(Directory.EnumerateFiles(root,"*.tmp"));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
