using System.Buffers.Binary;

namespace Aion2DPSPro.Protocol;

/// <summary>Reads the stable dbid preceding the observed player attribute block.</summary>
public static class OtherPlayerStableIdentity
{
    public static bool TryRead(ReadOnlySpan<byte> packet,int start,out long character,out int server)
    {
        character=0;server=0;
        if(start<0||start>packet.Length||packet.Length>32768)return false;
        int matches=0;
        for(int p=start;p+41<=packet.Length;p++)
        {
            ulong dbid=BinaryPrimitives.ReadUInt64LittleEndian(packet.Slice(p,8));
            long cid=(long)(dbid&0x0000FFFFFFFFFFFFUL);int sid=(int)(dbid>>48);
            int count=packet[p+8];
            if(cid is <1 or >uint.MaxValue||sid is <1001 or >9999||count is not (4 or 5))continue;
            int end=p+9+6*count;
            if(end+8>packet.Length)continue;
            // Capture-observed sorted attribute IDs; each has a fixed u32 value.
            // The optional D6 field distinguishes the two observed layouts.
            bool valid=true;
            for(int i=0;i<count;i++)
            {
                ushort expected=i switch {0=>0xCD,1=>0xCE,2=>0xD0,3 when count==5=>0xD6,_=>0x227};
                if(BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(p+9+6*i,2))!=expected){valid=false;break;}
            }
            if(!valid||BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(end,4)) is <1 or >60||
                BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(end+4,4))!=0)continue;
            matches++;character=cid;server=sid;
        }
        if(matches==1)return true;
        character=0;server=0;return false;
    }
}
