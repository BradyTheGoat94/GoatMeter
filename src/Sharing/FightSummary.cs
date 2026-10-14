using System.Globalization;
using System.Text;

namespace Aion2DPSPro.Sharing;
public static class FightSummary
{
    public static string Format(MeterSnapshot s,bool hideNames=false,bool includeScores=true)
    {
        string F(double value)=>value>=1000000?(value/1000000).ToString("0.00",CultureInfo.InvariantCulture)+"M":value>=1000?(value/1000).ToString("0.0",CultureInfo.InvariantCulture)+"K":value.ToString("0",CultureInfo.InvariantCulture);
        var target=s.Target?.Name??"Encounter";
        if(hideNames&&s.Players.Any(p=>p.Name==target))target="Player target";
        var output=new StringBuilder($"GoatMeter • {(s.PreviewMode?"SAMPLE DATA":"captured data")} • {target} • {TimeSpan.FromSeconds(s.FightSeconds):mm\\:ss} • {s.Category}\n");
        foreach(var pair in s.Players.Take(10).Select((p,i)=>(p,i)))
        {
            var p=pair.p;var name=hideNames?$"{p.ClassName} #{pair.i+1}":p.Name;
            output.Append($"{pair.i+1}. {name} — {F(p.Dps)} {s.MetricLabel} • total {F(p.Damage)} • {p.Share.ToString("0.0",CultureInfo.InvariantCulture)}%");
            if(includeScores&&(p.GearScore.HasValue||p.CombatPower.HasValue))output.Append($" • GS {(p.GearScore?.ToString(CultureInfo.InvariantCulture)??"—")} / CP {(p.CombatPower.HasValue?F(p.CombatPower.Value):"—")}");
            output.AppendLine();
        }
        if(s.Players.Count>10)output.AppendLine($"+{s.Players.Count-10} more players");
        return output.ToString().TrimEnd();
    }
}
