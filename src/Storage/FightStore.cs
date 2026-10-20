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
            return await JsonSerializer.DeserializeAsync<MeterSnapshot>(stream);
        }
        finally {ioGate.Release();}
    }
    public IEnumerable<string> List() => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder,"*.json").OrderByDescending(x=>x)
        : Enumerable.Empty<string>();
}
