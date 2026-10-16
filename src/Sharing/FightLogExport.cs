using System.Text.Json;
namespace Aion2DPSPro.Sharing;
// Character names and known servers are intentional. Raw events and stable IDs remain excluded.
public static class FightLogExport
{
    public static string Serialize(MeterSnapshot s)
    {
        if (s.InFight || s.PreviewMode || s.EncounterId == Guid.Empty || s.StartedUtc == null || string.IsNullOrWhiteSpace(s.EndReason) || !double.IsFinite(s.FightSeconds) || s.FightSeconds < 1 || s.FightSeconds > 7200)
            throw new InvalidOperationException("Export requires a completed captured encounter (1–7200 seconds), not Overall or sample data.");
        IReadOnlyList<PlayerStats> players = s.Categories.TryGetValue(MeterCategory.Damage, out var d) ? d.Players : s.Category == MeterCategory.Damage ? s.Players : Array.Empty<PlayerStats>();
        if (players.Count < 1 || players.Count > 24 || !players.Any(p => p.Damage > 0) || players.Any(p=>p.Damage<0 || p.Damage>1000000000000))
            throw new InvalidOperationException("Encounter has no valid exportable damage (1–24 players).");
        if (players.Any(p=>string.IsNullOrWhiteSpace(p.Name)||p.Name.Trim().Length>40||p.Name.Any(char.IsControl)))
            throw new InvalidOperationException("Each player needs a character name of 1–40 characters.");
        if (players.Select(p=>(p.Name.Trim(),Server:p.ServerId is >=1001 and <=9999?p.ServerId:0)).Distinct().Count()!=players.Count)
            throw new InvalidOperationException("This encounter contains repeated character names on the same or unknown server. Export a single completed fight.");
        var known = new HashSet<string> { "Gladiator", "Templar", "Assassin", "Ranger", "Sorcerer", "Spiritmaster", "Cleric", "Chanter" };
        var targets=s.Targets.Where(t=>!players.Any(p=>p.ActorId==t.TargetId)).GroupBy(t=>t.TargetId).Take(65)
            .Select((g,i)=>new {id=$"target-{i+1:00}",name=g.First().Name,durationSeconds=s.TargetSeconds.GetValueOrDefault(g.Key),damageByPlayer=players.Select(p=>g.Where(t=>t.ActorId==p.ActorId).Sum(t=>t.Damage)).ToArray()}).ToArray();
        if(targets.Length is <1 or >64 || targets.Any(t=>!double.IsFinite(t.durationSeconds)||t.durationSeconds<1||t.durationSeconds>s.FightSeconds||string.IsNullOrWhiteSpace(t.name)||t.name.Length>80||t.name.Any(char.IsControl)))
            throw new InvalidOperationException("Export requires named per-target damage (1–64 targets). Per-target timing is required; older history may need a new capture.");
        return JsonSerializer.Serialize(new {
            schemaVersion = 2,
            // The uploader explicitly selects one of the recorded targets for ranking.
            boss = "Unclassified encounter",
            startedUtc = s.StartedUtc.Value.ToUniversalTime().ToString("O"),
            durationSeconds = s.FightSeconds,
            visibility = "private",
            captureIntegrity = new {status=s.CaptureIntegrity.Status,droppedPackets=s.CaptureIntegrity.DroppedPackets,tcpGaps=s.CaptureIntegrity.TcpGaps},
            players = players.Select(p=>new {name=p.Name.Trim(),serverId=p.ServerId is >=1001 and <=9999?(int?)p.ServerId:null,className=known.Contains(p.ClassName)?p.ClassName:"Unknown",damage=p.Damage}),
            targets,
            rankingTargetId=(string?)null
        },new JsonSerializerOptions {WriteIndented=true});
    }
}
