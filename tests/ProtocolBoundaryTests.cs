using System;
using System.Collections.Generic;
using System.Linq;
using Aion2DPSPro.Protocol;
using Xunit;

namespace Aion2DPSPro.Tests;

public sealed class ProtocolBoundaryTests
{
    [Theory]
    [InlineData("324142430300","414243414243414243")]
    [InlineData("15410100","41414141414141414141")]
    public void Lz4OverlappingMatchesProduceExactBytes(string compressed,string expectedHex)
    {
        var expected=Convert.FromHexString(expectedHex);
        var output=new byte[expected.Length];
        Assert.Equal(output.Length,Lz4BlockDecoder.Decompress(Convert.FromHexString(compressed),output));
        Assert.Equal(expected,output);
    }

    [Theory]
    [InlineData("F0")]
    [InlineData("10")]
    [InlineData("104100")]
    [InlineData("10410000")]
    [InlineData("10410200")]
    [InlineData("1F410100")]
    [InlineData("10410100")]
    public void InvalidOrOversizedLz4BlocksRejectWithoutThrowing(string compressed)
    {
        Assert.Equal(-1,Lz4BlockDecoder.Decompress(Convert.FromHexString(compressed),new byte[4]));
    }

    private static readonly byte[] CapturedFrame=Convert.FromHexString("210438E3A00204008D3540B7B70009020B95C34701000000D658E7020100");
    private static ProtocolProfile Profile()=>new("test","Global","fixture",13328,
        new Dictionary<string,PacketTag>{{"damage",new(4,56)}});

    private static byte[] CompressedBatch(byte[] contents,int declaredSizeAdjustment=0)
    {
        // A literal-only LZ4 block: one long literal run, with no back-reference.
        Assert.InRange(contents.Length,15,269);
        var block=new byte[]{0xF0,(byte)(contents.Length-15)}.Concat(contents).ToArray();
        var length=1+2+4+block.Length;
        Assert.InRange(length+3,0,127);
        return new[]{(byte)(length+3),(byte)0xFF,(byte)0xFF}
            .Concat(BitConverter.GetBytes(contents.Length+declaredSizeAdjustment)).Concat(block).ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryTcpSplitPreservesCapturedBatchEventsExactlyOnce(bool compressed)
    {
        var contents=CapturedFrame.Concat(CapturedFrame).ToArray();
        var wire=compressed?CompressedBatch(contents):contents;
        var baseline=new CurrentClientDecoder(Profile()).Decode(contents,DateTime.UnixEpoch).ToArray();
        for(var split=1;split<wire.Length;split++)
        {
            var decoder=new CurrentClientDecoder(Profile());
            var actual=decoder.DecodeStream("in",wire[..split],DateTime.UnixEpoch)
                .Concat(decoder.DecodeStream("in",wire[split..],DateTime.UnixEpoch)).ToArray();
            Assert.Equal(baseline,actual);
            Assert.Equal(baseline[0],Assert.Single(decoder.DecodeStream("in",CapturedFrame,DateTime.UnixEpoch)));
        }
    }

    [Fact]
    public void InvalidCompressedBatchDoesNotSuppressFollowingValidFrames()
    {
        var decoder=new CurrentClientDecoder(Profile());
        var diagnostics=new List<DecoderDiagnostic>();
        decoder.Diagnostic += _=>throw new InvalidOperationException("diagnostic");
        decoder.Diagnostic += diagnostics.Add;
        var invalid=CompressedBatch(CapturedFrame,1);
        var actual=decoder.Decode(invalid.Concat(CapturedFrame).ToArray(),DateTime.UnixEpoch).ToArray();
        Assert.Equal(359,Assert.Single(actual).Amount);
        Assert.Contains(diagnostics,d=>d.Stage=="lz4");
        Assert.Equal(actual,decoder.Decode(CapturedFrame,DateTime.UnixEpoch).ToArray());
    }

    [Theory]
    [InlineData("00",0u)]
    [InlineData("7F",127u)]
    [InlineData("8001",128u)]
    [InlineData("FFFFFFFF0F",uint.MaxValue)]
    public void UintVarintsPreserveValidBoundaryValues(string hex,uint expected)
    {
        var bytes=Convert.FromHexString(hex);
        var p=0;
        Assert.True(ProtocolUtils.TryReadVarUInt(bytes,ref p,out var value,out var consumed));
        Assert.Equal(expected,value);
        Assert.Equal(bytes.Length,consumed);
        Assert.Equal(bytes.Length,p);
    }

    [Theory]
    [InlineData("8080808010")]
    [InlineData("FFFFFFFF7F")]
    [InlineData("8080808080")]
    public void UintVarintsRejectOverflowInsteadOfWrapping(string hex)
    {
        var p=0;
        Assert.False(ProtocolUtils.TryReadVarUInt(Convert.FromHexString(hex),ref p,out var value,out var consumed));
        Assert.Equal(0u,value);
        Assert.Equal(5,consumed);
        Assert.Equal(5,p);
    }

    [Theory]
    [InlineData("8080808080")]
    [InlineData("FFFFFFFF7F")]
    [InlineData("8080808010")]
    public void InvalidLengthHeaderDoesNotTrapFollowingValidFrames(string header)
    {
        var profile=new ProtocolProfile("test","Global","fixture",13328,
            new Dictionary<string,PacketTag>{{"damage",new(4,56)}});
        var decoder=new CurrentClientDecoder(profile);
        var invalid=Convert.FromHexString(header);
        var frame=Convert.FromHexString("210438E3A00204008D3540B7B70009020B95C34701000000D658E7020100");
        Assert.Empty(decoder.DecodeStream("in",invalid[..3],DateTime.UnixEpoch));
        var actual=decoder.DecodeStream("in",invalid[3..].Concat(frame).ToArray(),DateTime.UnixEpoch).ToArray();
        var baseline=new CurrentClientDecoder(profile).Decode(frame,DateTime.UnixEpoch).ToArray();
        Assert.Single(actual);
        Assert.Equal(baseline,actual);
    }
}
