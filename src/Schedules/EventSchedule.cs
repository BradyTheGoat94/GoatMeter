using System.Text.Json;
using System.IO;

namespace Aion2DPSPro.Schedules;

public sealed record ScheduleEvent(string Id,string Name,int[] Hours,int Minute,int EntryMinutes);
public sealed record ScheduleProfile(string Id,string Name,string TimeZoneId,string Source,string ReviewedUtc,ScheduleEvent[] Events)
{
    public override string ToString()=>Name;
}
public sealed record ScheduledWindow(ScheduleEvent Event,DateTimeOffset Start,DateTimeOffset End,bool InEntryWindow)
{
    public TimeSpan Remaining(DateTimeOffset now)=>(InEntryWindow?End:Start)-now;
}
public static class EventSchedule
{
    public static ScheduleProfile[] Load(string path)
    {
        var info=new FileInfo(path);
        if(info.Length>1_000_000)throw new InvalidDataException("Schedule file exceeds 1 MB.");
        var profiles=JsonSerializer.Deserialize<ScheduleProfile[]>(File.ReadAllText(path),new JsonSerializerOptions {PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Missing profiles.");
        if(profiles.Length is <1 or >20)throw new InvalidDataException("Use 1–20 schedule profiles.");
        foreach(var p in profiles)
        {
            if(p==null||string.IsNullOrWhiteSpace(p.Id)||p.Id.Length>80||string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>120||string.IsNullOrWhiteSpace(p.Source)||p.Source.Length>500||p.Events==null||p.Events.Length is <1 or >20)
                throw new InvalidDataException("Each profile needs a name, source and 1–20 events.");
            _=TimeZoneInfo.FindSystemTimeZoneById(p.TimeZoneId);
            if(p.Events.Any(e=>e==null||string.IsNullOrWhiteSpace(e.Id)||e.Id.Length>80||string.IsNullOrWhiteSpace(e.Name)||e.Name.Length>120||e.Hours==null||e.Hours.Length is <1 or >24||e.Hours.Any(h=>h is <0 or >23)||e.Minute is <0 or >59||e.EntryMinutes is <1 or >180))
                throw new InvalidDataException("Invalid event hours, minute or entry window.");
            if(p.Events.Select(e=>e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=p.Events.Length)throw new InvalidDataException("Duplicate event IDs.");
        }
        if(profiles.Select(p=>p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=profiles.Length)throw new InvalidDataException("Duplicate profile IDs.");
        return profiles;
    }
    public static IReadOnlyList<ScheduledWindow> Evaluate(ScheduleProfile profile,DateTimeOffset now)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId);
        var local=TimeZoneInfo.ConvertTime(now,zone).Date;
        var result=new List<ScheduledWindow>();
        foreach(var e in profile.Events)
        {
            var windows=new List<ScheduledWindow>();
            for(int day=-1;day<=2;day++)foreach(int hour in e.Hours.Distinct())
            {
                var wall=DateTime.SpecifyKind(local.AddDays(day).AddHours(hour).AddMinutes(e.Minute),DateTimeKind.Unspecified);
                if(zone.IsInvalidTime(wall))continue;
                var offsets=zone.IsAmbiguousTime(wall)?zone.GetAmbiguousTimeOffsets(wall):new[]{zone.GetUtcOffset(wall)};
                foreach(var offset in offsets)
                {
                    var start=new DateTimeOffset(wall,offset).ToUniversalTime();var end=start.AddMinutes(e.EntryMinutes);
                    windows.Add(new(e,start,end,start<=now&&now<end));
                }
            }
            var current=windows.Where(w=>w.InEntryWindow).OrderByDescending(w=>w.Start).FirstOrDefault();
            var next=current??windows.Where(w=>w.Start>now).OrderBy(w=>w.Start).FirstOrDefault();
            if(next!=null)result.Add(next);
        }
        return result.OrderBy(w=>w.InEntryWindow?0:1).ThenBy(w=>w.InEntryWindow?w.End:w.Start).ToArray();
    }
}
