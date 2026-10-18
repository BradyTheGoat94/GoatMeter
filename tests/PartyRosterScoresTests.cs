using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aion2DPSPro.Protocol;
using Xunit;

public class PartyRosterScoresTests
{
    // Synthetic layout only; no private names or capture bytes.
    static byte[] Roster(byte count=3,ushort server=2102,uint stable=12345)
    {
        var bytes=new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(123u));bytes.Add(0);
        bytes.Add(5);bytes.AddRange(new byte[17]);bytes.Add(count);
        bytes.Add(0x2c);bytes.Add(1);
        bytes.AddRange(BitConverter.GetBytes(((ulong)server<<48)|stable));
        var name=Encoding.UTF8.GetBytes("FixtureHero");bytes.Add((byte)name.Length);bytes.AddRange(name);
        bytes.AddRange(BitConverter.GetBytes(9u));bytes.AddRange(BitConverter.GetBytes(45u));bytes.AddRange(BitConverter.GetBytes(2800u));
        bytes.Add(1);bytes.AddRange(BitConverter.GetBytes(server));bytes.AddRange(BitConverter.GetBytes(server));bytes.Add(0);
        bytes.AddRange(BitConverter.GetBytes(121600UL));
        bytes.AddRange(new byte[22]);return bytes.ToArray();
    }

    [Fact]
    public void CompleteRosterPublishesMembershipSeparatelyFromScores()
    {
        var events=Dispatcher().Dispatch(Frame(new byte[]{2,0x97},Roster()),DateTime.UnixEpoch).ToArray();
        var roster=Assert.Single(events,x=>x.Kind==Aion2DPSPro.CombatKind.PartyRoster);
        var member=Assert.Single(roster.PartyMembers!);
        Assert.Equal(12345,member.CharacterId);Assert.Equal(2102,member.ServerId);
        Assert.DoesNotContain(Dispatcher().Dispatch(Frame(new byte[]{2,0x97},Roster()[..30]),DateTime.UnixEpoch),x=>x.Kind==Aion2DPSPro.CombatKind.PartyRoster);
    }

    [Fact]
    public void LatestRosterReplacesMembersWithoutErasingScoreCache()
    {
        var bridge=new Aion2DPSPro.Capture.CaptureIdentityBridge();var t=DateTime.UnixEpoch;
        bridge.Observe("game",new(t,Aion2DPSPro.CombatKind.CharacterStats,Source:"CachedHero",SourceCharacterId:2,SourceServerId:2102,SourceGearScore:2800,StatsObservedUtc:t));
        bridge.Observe("game",new(t,Aion2DPSPro.CombatKind.PartyRoster,PartyMembers:new[]{new Aion2DPSPro.PartyMember(1,2102),new Aion2DPSPro.PartyMember(2,2102)}));
        bridge.Observe("game",new(t.AddSeconds(1),Aion2DPSPro.CombatKind.PartyRoster,PartyMembers:new[]{new Aion2DPSPro.PartyMember(1,2102)}));
        bridge.Observe("game",new(t,Aion2DPSPro.CombatKind.PartyRoster,PartyMembers:new[]{new Aion2DPSPro.PartyMember(2,2102)}));
        var roster=Assert.Single(bridge.Identities("game",t.AddSeconds(2)));
        Assert.Equal(1,Assert.Single(roster.PartyMembers!).CharacterId);
        var cached=bridge.Resolve("game",new(t.AddSeconds(2),Aion2DPSPro.CombatKind.PlayerName,Source:"CachedHero",SourceCharacterId:2,SourceServerId:2102));
        Assert.Equal(2800,cached.SourceGearScore);
        Assert.Empty(bridge.Identities("other",t.AddSeconds(2)));
        Assert.Empty(bridge.Identities("game",t.AddMinutes(11)));
        bridge.Clear();Assert.Empty(bridge.Identities("game",t.AddSeconds(2)));
    }

    [Fact]
    public void CompleteVacantSlotsPreserveScores()
    {
        var row=Assert.Single(PartyRosterScores.Parse(Roster(),DateTime.UnixEpoch));
        Assert.Equal(2800,row.GearScore);Assert.Equal(121600,row.CombatPower);
    }

    [Fact]
    public void MissingAdvertisedVacantSlotRejectsWholeSnapshot()
    {
        Assert.Empty(PartyRosterScores.Parse(Roster().AsSpan(0,Roster().Length-1),DateTime.UnixEpoch));
    }

    [Fact]
    public void OccupiedIdentityAfterVacantSlotRejectsWholeSnapshot()
    {
        var bytes=Roster();bytes[^9]=1;
        Assert.Empty(PartyRosterScores.Parse(bytes,DateTime.UnixEpoch));
    }

    [Fact]
    public void NumberedVacantSlotsUseCapturedStride()
    {
        var bytes=Roster().SkipLast(22).ToList();
        for(byte slot=2;slot<=3;slot++)
        {
            var vacant=new byte[35];vacant[1]=slot;vacant[23]=slot;bytes.AddRange(vacant);
        }
        Assert.Single(PartyRosterScores.Parse(bytes.ToArray(),DateTime.UnixEpoch));
        bytes.RemoveAt(bytes.Count-1);
        Assert.Empty(PartyRosterScores.Parse(bytes.ToArray(),DateTime.UnixEpoch));
    }

    [Fact]
    public void OccupiedMemberAfterVacancyIsDecoded()
    {
        var first=Roster();var bytes=first.SkipLast(22).ToList();
        var vacant=new byte[35];vacant[1]=2;vacant[23]=1;bytes.AddRange(vacant);
        var last=first.Skip(24).SkipLast(22).ToArray();last[1]=3;last[2]++;
        bytes.AddRange(last);
        var rows=PartyRosterScores.Parse(bytes.ToArray(),DateTime.UnixEpoch);
        Assert.Equal(2,rows.Count);Assert.NotEqual(rows[0].CharacterId,rows[1].CharacterId);
        Assert.All(rows,row=>Assert.Equal(121600,row.CombatPower));
    }

    static byte[] Attributes(uint stable=12345,ushort server=2102,bool optional=true)
    {
        var bytes=new List<byte>(BitConverter.GetBytes(((ulong)server<<48)|stable));
        ushort[] tags=optional?new ushort[]{0xCD,0xCE,0xD0,0xD6,0x227}:new ushort[]{0xCD,0xCE,0xD0,0x227};
        bytes.Add((byte)tags.Length);
        foreach(var tag in tags){bytes.AddRange(BitConverter.GetBytes(tag));bytes.AddRange(BitConverter.GetBytes(900u));}
        bytes.AddRange(BitConverter.GetBytes(45u));bytes.AddRange(new byte[4]);return bytes.ToArray();
    }
    static byte[] Varint(uint value)
    {
        var bytes=new List<byte>();do{byte b=(byte)(value&127);value>>=7;bytes.Add((byte)(b|(value>0?128:0)));}while(value>0);return bytes.ToArray();
    }
    static byte[] Frame(params byte[][] parts)
    {
        var body=parts.SelectMany(x=>x).ToArray();return Varint((uint)body.Length+4).Concat(body).ToArray();
    }
    static byte[] OtherInfo(byte[] attributes,string name="FixtureHero",uint actor=7001)
        =>Frame(new byte[]{0x45,0x36},Varint(actor),new byte[]{1,0x20,0xA0,1,0x17,(byte)name.Length},Encoding.UTF8.GetBytes(name),new byte[]{9,0,0,0,0},attributes);
    static PacketDispatcher Dispatcher()=>new(new ProtocolProfile("test","Global","fixture",13328,new Dictionary<string,PacketTag>{{"entityRemoved",new(0x21,0x8D)}},ExperimentalRosterScores:true));
    static void ObserveRoster(PacketDispatcher d,ushort server=2102,uint stable=12345)
        =>d.Dispatch(Frame(new byte[]{2,0x97},Roster(server:server,stable:stable)),DateTime.UnixEpoch).ToArray();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OtherPlayerGetsScoresWithoutLocalSelfOrSessionLink(bool optional)
    {
        var d=Dispatcher();ObserveRoster(d);
        var e=d.Dispatch(OtherInfo(Attributes(optional:optional)),DateTime.UnixEpoch).Last(x=>x.Kind==Aion2DPSPro.CombatKind.PlayerName);
        var linked=d.AttachRosterScores(e,DateTime.UnixEpoch);
        Assert.Equal(7001,linked.SourceId);Assert.Equal(12345,linked.SourceCharacterId);Assert.Equal(2102,linked.SourceServerId);
        Assert.Equal(2800,linked.SourceGearScore);Assert.Equal(121600,linked.SourceCombatPower);
    }

    [Fact]
    public void RosterArrivingAfterOtherInfoRefreshesCombatActor()
    {
        var d=Dispatcher();d.Dispatch(OtherInfo(Attributes()),DateTime.UnixEpoch).ToArray();
        var events=d.Dispatch(Frame(new byte[]{2,0x97},Roster()),DateTime.UnixEpoch).ToArray();
        Assert.Contains(events,x=>x.SourceId==7001&&x.SourceGearScore==2800);
    }

    [Fact]
    public void SameNameOnWrongServerCannotSupplyScores()
    {
        var d=Dispatcher();ObserveRoster(d,server:2202);
        var e=d.Dispatch(OtherInfo(Attributes()),DateTime.UnixEpoch).Last(x=>x.Kind==Aion2DPSPro.CombatKind.PlayerName);
        Assert.Null(d.AttachRosterScores(e,DateTime.UnixEpoch).SourceGearScore);
    }

    [Fact]
    public void BareDbidOrAmbiguousAttributeBlocksCannotSupplyIdentity()
    {
        Assert.False(OtherPlayerStableIdentity.TryRead(Attributes()[..8],0,out _,out _));
        var two=Attributes().Concat(Attributes(stable:54321)).ToArray();
        Assert.False(OtherPlayerStableIdentity.TryRead(two,0,out _,out _));
        var corrupt=Attributes();corrupt[9]++;
        Assert.False(OtherPlayerStableIdentity.TryRead(corrupt,0,out _,out _));
        for(int n=0;n<Attributes().Length;n++)
            Assert.False(OtherPlayerStableIdentity.TryRead(Attributes().AsSpan(0,n),0,out _,out _));
    }

    [Fact]
    public void RenamedActorCannotInheritPreviousStableIdentity()
    {
        var d=Dispatcher();ObserveRoster(d);d.Dispatch(OtherInfo(Attributes()),DateTime.UnixEpoch).ToArray();
        var e=d.Dispatch(OtherInfo(Array.Empty<byte>(),"Replacement"),DateTime.UnixEpoch).Last(x=>x.Kind==Aion2DPSPro.CombatKind.PlayerName);
        var linked=d.AttachRosterScores(e,DateTime.UnixEpoch);
        Assert.Equal(0,linked.SourceCharacterId);Assert.Null(linked.SourceGearScore);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SiblingSocketScoresReachActualCombatSnapshot(bool rosterFirst)
    {
        var profile=new ProtocolProfile("test","Global","fixture",13328,new Dictionary<string,PacketTag>{{"damage",new(4,0x38)}},ExperimentalRosterScores:true);
        using var capture=new Aion2DPSPro.Capture.LiveCaptureAdapter(new CurrentClientDecoder(profile));
        var clock=DateTime.UnixEpoch;var engine=new Aion2DPSPro.CombatEngine(()=>clock);
        capture.EventReceived+=engine.Apply;
        var memberships=new List<Aion2DPSPro.CombatEvent>();
        capture.EventReceived+=e=>{if(e.Kind==Aion2DPSPro.CombatKind.PartyRoster)memberships.Add(e);};
        var process=typeof(Aion2DPSPro.Capture.LiveCaptureAdapter).GetMethod("Process",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        void Send(ushort port,byte[] payload)
        {
            var ip=new PacketDotNet.IPv4Packet(System.Net.IPAddress.Parse("10.0.0.2"),System.Net.IPAddress.Parse("10.0.0.1"));
            ip.PayloadPacket=new PacketDotNet.TcpPacket(13328,port){SequenceNumber=100,PayloadData=payload};
            process.Invoke(capture,new object[]{"test-adapter",ip,clock});
        }
        var roster=Frame(new byte[]{2,0x97},Roster());
        if(rosterFirst)Send(50001,roster);
        Send(50002,OtherInfo(Attributes(),actor:6797));
        Send(50003,Convert.FromHexString("210438E3A00204008D3540B7B70009020B95C34701000000D658E7020100"));
        if(!rosterFirst)Send(50004,roster);
        Assert.NotEmpty(memberships);
        Assert.Equal(12345,Assert.Single(memberships.Last().PartyMembers!).CharacterId);
        var player=Assert.Single(engine.Snapshot().Players);
        Assert.Equal(6797,player.ActorId);Assert.Equal("FixtureHero",player.Name);
        Assert.Equal(2800,player.GearScore);Assert.Equal(121600,player.CombatPower);
        Assert.Equal(359,player.Damage);
    }

    [Fact]
    public void ExpiredRosterCannotAttachScores()
    {
        var d=Dispatcher();ObserveRoster(d);
        var e=d.Dispatch(OtherInfo(Attributes()),DateTime.UnixEpoch).Last(x=>x.Kind==Aion2DPSPro.CombatKind.PlayerName);
        Assert.Null(d.AttachRosterScores(e,DateTime.UnixEpoch.AddMinutes(11)).SourceGearScore);
    }

    [Fact]
    public void VisibilityRemovalPreservesKnownPlayerLink()
    {
        var d=Dispatcher();ObserveRoster(d);
        var e=d.Dispatch(OtherInfo(Attributes()),DateTime.UnixEpoch).Last(x=>x.Kind==Aion2DPSPro.CombatKind.PlayerName);
        d.Dispatch(Frame(new byte[]{0x21,0x8D},Varint(7001)),DateTime.UnixEpoch).ToArray();
        var unknown=e with {SourceCharacterId=0,SourceServerId=0};
        Assert.Equal(2800,d.AttachRosterScores(unknown,DateTime.UnixEpoch).SourceGearScore);
    }

    [Fact]
    public void ConflictingExplicitSessionIdentityBlocksScoreJoin()
    {
        var d=Dispatcher();ObserveRoster(d);
        var e=d.Dispatch(OtherInfo(Attributes()),DateTime.UnixEpoch).Last(x=>x.Kind==Aion2DPSPro.CombatKind.PlayerName);
        d.Dispatch(Frame(new byte[]{0x20,0x36,0,0},Varint(7001),new byte[4],BitConverter.GetBytes(54321u)),DateTime.UnixEpoch).ToArray();
        var joined=d.AttachRosterScores(e,DateTime.UnixEpoch);
        Assert.Null(joined.SourceGearScore);Assert.Equal(0,joined.SourceCharacterId);
    }
}
