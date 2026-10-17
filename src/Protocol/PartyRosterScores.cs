using System.Buffers.Binary;
using System.Text;

namespace Aion2DPSPro.Protocol;

// Independent, conservative reader of the reported Global roster layout.
// Reject ambiguity/partial records; this is separate from combat decoding.
public sealed record RosterScore(long CharacterId,int ServerId,string Name,int ClassCode,int? GearScore,long? CombatPower,DateTime ObservedUtc);
public static class PartyRosterScores
{
    static readonly UTF8Encoding Utf8=new(false,true);
    public static IReadOnlyList<RosterScore> Parse(ReadOnlySpan<byte> body,DateTime utc)
    {
        var empty=Array.Empty<RosterScore>();
        if(body.Length is <25 or >32768)return empty;
        int labelLength=body[4];int p=5+labelLength;
        if(labelLength>64||p+18>body.Length||body[p] is <1 or >12)return empty;
        try{_=Utf8.GetString(body.Slice(5,labelLength));}catch(DecoderFallbackException){return empty;}
        p+=18;
        if(!ProtocolUtils.TryReadVarUInt(body,ref p,out var count,out _)||count is <1 or >12)return empty;
        var rows=new List<RosterScore>();var used=new HashSet<(long,int)>();
        byte priorSlot=0;
        for(uint member=0;member<count;member++)
        {
            if(p+11>body.Length)return empty;
            byte slot=body[p+1];var dbid=BinaryPrimitives.ReadUInt64LittleEndian(body.Slice(p+2,8));int length=body[p+10];
            if(length==0)
            {
                if(slot!=0)
                {
                    // Live captures contain 35-byte vacant slots between
                    // occupied members; vacancy does not terminate the roster.
                    if(slot!=priorSlot+1||dbid!=0||p+35>body.Length||body.Slice(p+2,21).IndexOfAnyExcept((byte)0)>=0)return empty;
                    priorSlot=slot;p+=35;continue;
                }
                // A compact zero sentinel terminates the occupied roster, but every
                // advertised remaining slot must still be present and vacant.
                // Otherwise a truncated or contradictory snapshot could expose
                // a partial membership list to the Party filter.
                int remaining=checked((int)(count-member));
                // Retain compact zero-filled vacant headers used by fixtures.
                int stride=11;
                if(p+remaining*stride>body.Length)return empty;
                for(int vacant=0;vacant<remaining;vacant++)
                {
                    int q=p+vacant*stride;
                    if(body[q+1]!=0||BinaryPrimitives.ReadUInt64LittleEndian(body.Slice(q+2,8))!=0||body[q+10]!=0)return empty;
                }
                break;
            }
            int server=(int)(dbid>>48);long character=(long)(dbid&0x0000FFFFFFFFFFFFUL);
            if(slot<=priorSlot||slot>12||server is <1001 or >9999||character is <1 or >uint.MaxValue||length is <2 or >48||p+11+length+12>body.Length)return empty;
            string name;try{name=Utf8.GetString(body.Slice(p+11,length));}catch(DecoderFallbackException){return empty;}
            if(name.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))return empty;
            int values=p+11+length;uint job=BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(values,4));uint level=BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(values+4,4));uint gear=BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(values+8,4));
            if(job is <5 or >40||level is <1 or >60)return empty;
            int flags=values+12;
            if((body[p]&1)!=0&&gear<100&&flags+4<=body.Length)
            {gear=BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(flags,4));flags+=4;}
            if(gear!=0&&gear is <100 or >50000)return empty;
            // Require both server words and a small role before CP. Never find
            // an arbitrary plausible number somewhere in the member tail.
            var anchors=new List<int>();
            for(int a=flags;a<=Math.Min(flags+4,body.Length-13);a++)
                if(BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(a,2))==server&&BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(a+2,2)) is >=1001 and <=9999&&body[a+4]<=12&&BinaryPrimitives.ReadUInt64LittleEndian(body.Slice(a+5,8))<=10000000UL)anchors.Add(a);
            if(anchors.Count!=1)return empty;
            int anchor=anchors[0];var power=BinaryPrimitives.ReadUInt64LittleEndian(body.Slice(anchor+5,8));
            if(!used.Add((character,server)))return empty;
            rows.Add(new(character,server,name,(int)job,gear==0?null:(int)gear,power==0?null:(long)power,utc));priorSlot=slot;
            p=anchor+13;
            if(member+1<count)
            {
                int next=-1;
                for(int q=p;q<=Math.Min(p+64,body.Length-11);q++)
                {
                    if(body[q+1]!=(byte)(slot+1))continue;
                    var nextDbid=BinaryPrimitives.ReadUInt64LittleEndian(body.Slice(q+2,8));int n=body[q+10];
                    if(n==0&&nextDbid==0&&q+35<=body.Length&&body.Slice(q+2,21).IndexOfAnyExcept((byte)0)<0){if(next>=0)return empty;next=q;continue;}
                    if((nextDbid>>48) is >=1001 and <=9999 && (nextDbid&0x0000FFFFFFFFFFFFUL)>0 && n is >=2 and <=48 && q+11+n+12<=body.Length)
                    {if(next>=0)return empty;next=q;}
                }
                if(next<0)
                    for(int q=p;q<=Math.Min(p+64,body.Length-11);q++)
                        if(body.Slice(q,11).IndexOfAnyExcept((byte)0)<0 && body.Length-q>=(count-member-1)*11)
                        {next=q;break;}
                if(next<0)return empty;p=next;
            }
        }
        return rows;
    }
}
