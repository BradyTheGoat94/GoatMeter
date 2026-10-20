using System;
using System.Collections.Generic;
using System.Linq;
using Aion2DPSPro.Protocol;
using Xunit;

namespace Aion2DPSPro.Tests;

public sealed class ProtocolBoundaryTests
{
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
