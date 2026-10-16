namespace Aion2DPSPro;
public static class CharacterScoreDisplay
{
    public static string Describe(PlayerStats player,DateTime now,bool historical=false)
    {
        if(player.StatsObservedUtc is not DateTime seen)return "GS/CP not observed. Waiting for an exact character/server roster match.";
        string identity=player.ServerId>0?$"Server {player.ServerId}":"Server unknown";
        if(historical)return $"Captured roster scores (experimental) • {identity} • observed {seen.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC. Frozen encounter snapshot.";
        var age=now-seen;
        if(age<TimeSpan.Zero)return "Score observation time is in the future; values unavailable.";
        if(age>TimeSpan.FromMinutes(10))return $"Roster scores expired • {identity} • observed {seen.ToUniversalTime():HH:mm:ss} UTC. Waiting for refreshed metadata.";
        string available=player.GearScore.HasValue||player.CombatPower.HasValue?"Captured roster scores (experimental)":"Roster observed; score values unavailable";
        return $"{available} • {identity} • {Math.Floor(age.TotalSeconds):0}s old. Values are not estimates.";
    }
}
