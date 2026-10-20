namespace Aion2DPSPro.Protocol;

public static class ProtocolUtils
{
    public static bool TryReadVarUInt(ReadOnlySpan<byte> data, ref int p, out uint value, out int consumed)
    {
        value = 0; consumed = 0; int shift = 0;
        while (p < data.Length && consumed < 5)
        {
            byte b = data[p++]; consumed++;
            // A uint has only four payload bits left in its fifth byte.
            if(consumed==5 && (b&0xF0)!=0) {value=0;return false;}
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
        }
        value = 0; return false;
    }
}
