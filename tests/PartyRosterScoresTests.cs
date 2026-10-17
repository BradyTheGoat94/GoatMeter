using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aion2DPSPro.Protocol;
using Xunit;

public class PartyRosterScoresTests
{
    // Synthetic layout only; no private names or capture bytes.
    static byte[] Roster(byte count=3)
    {
        const ushort server=2102;
        var bytes=new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(123u));bytes.Add(0);
        bytes.Add(5);bytes.AddRange(new byte[17]);bytes.Add(count);
        bytes.Add(0x2c);bytes.Add(1);
        bytes.AddRange(BitConverter.GetBytes(((ulong)server<<48)|12345));
        var name=Encoding.UTF8.GetBytes("FixtureHero");bytes.Add((byte)name.Length);bytes.AddRange(name);
        bytes.AddRange(BitConverter.GetBytes(9u));bytes.AddRange(BitConverter.GetBytes(45u));bytes.AddRange(BitConverter.GetBytes(2800u));
        bytes.Add(1);bytes.AddRange(BitConverter.GetBytes(server));bytes.AddRange(BitConverter.GetBytes(server));bytes.Add(0);
        bytes.AddRange(BitConverter.GetBytes(121600UL));
        bytes.AddRange(new byte[22]);return bytes.ToArray();
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
}
