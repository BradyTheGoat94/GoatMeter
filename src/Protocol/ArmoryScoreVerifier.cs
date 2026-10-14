using System.Text.Json;

namespace Aion2DPSPro.Protocol;

/// <summary>
/// Read-only parsing of user-supplied character profile JSON. The source is not authenticated.
/// Never replaces passive in-game values, never performs network requests.
/// The external web characterId is opaque and is NOT the in-game numeric actor ID.
/// </summary>
public sealed record ArmoryScore(string CharacterId, int ServerId, int? GearScore,
    long? CombatPower, DateTime ObservedUtc, string Provenance);

public static class ArmoryScoreVerifier
{
    public static bool TryParse(string json, string expectedCharacterId, int expectedServerId,
        DateTime observedUtc, out ArmoryScore? score)
    {
        score=null;
        if(string.IsNullOrWhiteSpace(json)||json.Length>1_000_000||
           string.IsNullOrWhiteSpace(expectedCharacterId)||expectedServerId<=0||
           observedUtc.Kind!=DateTimeKind.Utc)return false;
        try
        {
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions { MaxDepth=24,AllowTrailingCommas=false,CommentHandling=JsonCommentHandling.Disallow });
            var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object)return false;
            if(!root.TryGetProperty("profile",out var profile)||profile.ValueKind!=JsonValueKind.Object)return false;
            if(!(profile.TryGetProperty("characterId",out var id)||root.TryGetProperty("characterId",out id))||
               id.ValueKind!=JsonValueKind.String||
               !string.Equals(id.GetString(),expectedCharacterId,StringComparison.Ordinal))return false;
            if(root.TryGetProperty("serverId",out var server)&&
               (!TryInt(server,out var serverId)||serverId!=expectedServerId))return false;
            long? cp=null;int? gs=null;
            if(profile.TryGetProperty("combatPower",out var cpValue))
            {
                if(!TryLong(cpValue,out var n)||n<1||n>10_000_000)return false;
                cp=n;
            }
            if(root.TryGetProperty("stat",out var stat)&&stat.ValueKind==JsonValueKind.Object &&
               stat.TryGetProperty("statList",out var list)&&list.ValueKind==JsonValueKind.Array)
            {
                foreach(var item in list.EnumerateArray())
                {
                    if(item.ValueKind!=JsonValueKind.Object||
                       !item.TryGetProperty("type",out var type)||
                       type.ValueKind!=JsonValueKind.String||
                       type.GetString()!="ItemLevel")continue;
                    if(gs.HasValue||!item.TryGetProperty("value",out var value)||
                       !TryInt(value,out var number)||number<100||number>50_000)return false;
                    gs=number;
                }
            }
            if(cp==null&&gs==null)return false;
            score=new(expectedCharacterId,expectedServerId,gs,cp,observedUtc,"unverified-user-profile-import");
            return true;
        }
        catch(JsonException){return false;}
    }
    private static bool TryInt(JsonElement e,out int value)=>
        e.ValueKind==JsonValueKind.Number?e.TryGetInt32(out value):
        int.TryParse(e.ValueKind==JsonValueKind.String?e.GetString():null,
            System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out value);
    private static bool TryLong(JsonElement e,out long value)=>
        e.ValueKind==JsonValueKind.Number?e.TryGetInt64(out value):
        long.TryParse(e.ValueKind==JsonValueKind.String?e.GetString():null,
            System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out value);
}
