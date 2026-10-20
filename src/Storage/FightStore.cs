using System.IO;
using System.Text.Json;

namespace Aion2DPSPro.Storage;

public sealed class FightStore
{
    private readonly string folder;
    private readonly System.Threading.SemaphoreSlim ioGate=new(1,1);
    public FightStore(string? folder = null)
    {
        this.folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aion2DPSPro", "History");
    }
    public async Task SaveAsync(MeterSnapshot snapshot)
    {
        await ioGate.WaitAsync();
        try
        {
            // Optional persistence must not prevent the meter from starting. Retry
            // directory creation on each save so a repaired location can recover.
            Directory.CreateDirectory(folder);
            var path=Path.Combine(folder,$"{snapshot.StartedUtc:yyyyMMdd_HHmmss_fff}_{snapshot.EncounterId:N}.json");
            var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(snapshot,new JsonSerializerOptions {WriteIndented=true}));
                File.Move(temp,path,true);
            }
            finally
            {
                try {File.Delete(temp);}
                catch(IOException) { }
                catch(UnauthorizedAccessException) { }
            }
        }
        finally {ioGate.Release();}
    }
    public async Task<MeterSnapshot?> LoadAsync(string path)
    {
        // Serialize this store's reads and replacements: Windows can otherwise
        // leave a just-replaced name pending deletion while readers close it.
        await ioGate.WaitAsync();
        try
        {
            // Permit replacement by another store while retaining a complete
            // file for this reader; this store's own writes wait for ioGate.
            await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete,4096,true);
            var snapshot=await JsonSerializer.DeserializeAsync<MeterSnapshot>(stream);
            if(snapshot!=null)ValidateSnapshot(snapshot);
            return snapshot;
        }
        finally {ioGate.Release();}
    }
    private static bool HasItems<T>(IReadOnlyList<T>? items) where T:class =>
        items!=null && items.All(item=>item!=null);
    private static bool ValidPlayers(IReadOnlyList<PlayerStats>? players) =>
        HasItems(players) && players!.All(player=>player.ClassName!=null);
    private static bool ValidSkills(IReadOnlyList<SkillStats>? skills) =>
        HasItems(skills) && skills!.All(skill=>skill.FlagHits!=null);
    private static void ValidateSnapshot(MeterSnapshot snapshot)
    {
        // JSON nulls bypass C# non-null annotations. Reject broken structures
        // before a saved selection reaches recurring UI/report callbacks.
        if(!ValidPlayers(snapshot.Players) || !ValidSkills(snapshot.Skills) ||
           !HasItems(snapshot.Buffs) || !HasItems(snapshot.RecentEvents) || !HasItems(snapshot.Targets) ||
           snapshot.Categories==null || snapshot.Categories.Values.Any(category=>
               category==null || !ValidPlayers(category.Players) || !ValidSkills(category.Skills)))
            throw new InvalidDataException("Saved fight history contains missing collections or invalid rows.");
        if(snapshot.FightSeconds<0)
            throw new InvalidDataException("Saved fight history has a negative duration.");
        try {_=TimeSpan.FromSeconds(snapshot.FightSeconds);}
        catch(Exception ex) when(ex is ArgumentException or OverflowException)
        {throw new InvalidDataException("Saved fight history has an invalid duration.",ex);}
    }
    public IEnumerable<string> List() => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder,"*.json").OrderByDescending(x=>x)
        : Enumerable.Empty<string>();
}
