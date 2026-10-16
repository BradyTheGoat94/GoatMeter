using System.IO;
using System.Text.Json;
namespace Aion2DPSPro.Storage;

public sealed class FightStore
{
    private readonly string folder;
    private readonly SemaphoreSlim writes=new(1,1);
    public const long MaxFileBytes=16*1024*1024;
    public const long MaxStorageBytes=512L*1024*1024;
    public int RetentionCount {get;private set;}=500;
    public event Action<string>? Warning;
    public FightStore(string? folder=null)
    {
        this.folder=Path.GetFullPath(folder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aion2DPSPro","History"));
        Directory.CreateDirectory(this.folder);
        try{
            var settings=Path.Combine(this.folder,"history-settings.json");
            if(File.Exists(settings)&&new FileInfo(settings).Length<=1024){var n=JsonSerializer.Deserialize<int>(File.ReadAllText(settings));if(n is >=50 and <=5000)RetentionCount=n;}
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){Warning?.Invoke("History settings could not be read; using 500 active fights.");}
    }
    public async Task SetRetentionAsync(int count)
    {
        if(count is <50 or >5000)throw new ArgumentOutOfRangeException(nameof(count));
        await writes.WaitAsync();
        try{var temp=Path.Combine(folder,"history-settings.json.tmp");try{await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(count));File.Move(temp,Path.Combine(folder,"history-settings.json"),true);}finally{if(File.Exists(temp))File.Delete(temp);}RetentionCount=count;ArchiveOldest();}finally{writes.Release();}
    }
    public async Task SaveAsync(MeterSnapshot snapshot)
    {
        await writes.WaitAsync();
        string? temp=null;
        try{
            var files=Directory.EnumerateFiles(folder,"*.json",SearchOption.AllDirectories).Take(10001).Select(p=>new FileInfo(p)).ToArray();
            if(files.Length>=10000)throw new IOException("History file budget reached. Move archived fights to another location before saving more.");
            var bytes=JsonSerializer.SerializeToUtf8Bytes(snapshot,new JsonSerializerOptions{WriteIndented=true});
            if(bytes.Length>MaxFileBytes)throw new IOException("Encounter exceeds the 16 MiB history file limit.");
            if(files.Sum(x=>x.Length)+bytes.Length>MaxStorageBytes)throw new IOException("History reached its 512 MiB storage budget. Move archived fights elsewhere before saving more.");
            var path=Path.Combine(folder,$"{snapshot.StartedUtc:yyyyMMdd_HHmmss_fff}_{snapshot.EncounterId:N}.json");temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            await File.WriteAllBytesAsync(temp,bytes);File.Move(temp,path,true);ArchiveOldest();
        }finally{if(temp!=null&&File.Exists(temp))File.Delete(temp);writes.Release();}
    }
    private void ArchiveOldest()
    {
        var old=List().Skip(RetentionCount).ToArray();if(old.Length==0)return;
        var archive=Path.Combine(folder,"Archive");Directory.CreateDirectory(archive);
        foreach(var file in old){var target=Path.Combine(archive,Path.GetFileName(file));if(File.Exists(target))target=Path.Combine(archive,Guid.NewGuid().ToString("N")+"_"+Path.GetFileName(file));File.Move(file,target);}
    }
    public async Task<MeterSnapshot?> LoadAsync(string path)
    {
        var full=Path.GetFullPath(path);
        if(!full.StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("History file must be inside the history folder.");
        try{
            var info=new FileInfo(full);if(info.LinkTarget!=null||info.Length>MaxFileBytes){Warning?.Invoke("Skipped oversized or linked history file: "+info.Name);return null;}
            await using var stream=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.Read,4096,true);
            // Read at most the budget plus one byte even if another process grows the file.
            using var bounded=new MemoryStream();var buffer=new byte[8192];int n;
            while((n=await stream.ReadAsync(buffer))>0){if(bounded.Length+n>MaxFileBytes){Warning?.Invoke("Skipped history file exceeding its size budget.");return null;}await bounded.WriteAsync(buffer.AsMemory(0,n));}
            bounded.Position=0;var snapshot=await JsonSerializer.DeserializeAsync<MeterSnapshot>(bounded);
            if(snapshot==null||snapshot.Players==null||snapshot.Skills==null||snapshot.Buffs==null||snapshot.RecentEvents==null||snapshot.Categories==null||snapshot.Targets==null||snapshot.TargetSeconds==null||snapshot.CaptureIntegrity==null||snapshot.Players.Any(p=>p==null)||snapshot.Skills.Any(p=>p==null)||snapshot.Categories.Values.Any(c=>c==null||c.Players==null||c.Skills==null)||!double.IsFinite(snapshot.FightSeconds)||snapshot.FightSeconds<0){Warning?.Invoke("Skipped invalid history record: "+info.Name);return null;}
            return snapshot;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException){Warning?.Invoke("Skipped unreadable history file: "+Path.GetFileName(full));return null;}
    }
    public IEnumerable<string> List()=>Directory.EnumerateFiles(folder,"*.json").Where(p=>Path.GetFileName(p)!="history-settings.json").OrderByDescending(Path.GetFileName);
}
