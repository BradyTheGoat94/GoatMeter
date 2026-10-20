using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aion2DPSPro.Storage;
using Xunit;

namespace Aion2DPSPro.Tests;

public sealed class StorageRegressionTests
{
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
