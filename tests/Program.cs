using Aion2DPSPro;
using Aion2DPSPro.Protocol;
using Aion2DPSPro.Storage;

int checks=0;
void Equal(double actual,double expected,string label) {checks++;if(Math.Abs(actual-expected)>.00001)throw new Exception($"{label}: expected {expected}, got {actual}");}
void True(bool actual,string label) {checks++;if(!actual)throw new Exception(label);}
var t=new DateTime(2026,10,3,0,0,0,DateTimeKind.Utc);
var now=t;
var e=new CombatEngine(()=>now);
CombatEvent Hit(int seconds,long id,long amount,string name="Player",bool boss=false)=>new(t.AddSeconds(seconds),CombatKind.Damage,id,name,99,"Boss","Strike",amount,IsBoss:boss);
e.Apply(new(t,CombatKind.PlayerName,1,"Player"));e.Apply(Hit(0,1,100));e.Apply(Hit(5,1,200));
Equal(e.Snapshot().FightDamage,300,"damage sum");Equal(e.Snapshot().FightDps,60,"encounter DPS");
now=t.AddSeconds(7);Equal(e.Snapshot().FightDps,60,"DPS frozen during idle");
e.Apply(new(t.AddSeconds(2),CombatKind.BuffApply,1,"Player",1,"Player",Effect:"Power",Stacks:1));
e.Apply(new(t.AddSeconds(6),CombatKind.BuffRemove,1,"Player",1,"Player",Effect:"Power"));
// Late timestamps must not reset encounter activity.
now=t.AddSeconds(13);True(!e.Snapshot().InFight,"timeout completes");Equal(e.Snapshot(MeterSegment.Previous,MeterCategory.Damage).FightDamage,300,"completed preserved");Equal(e.History.Count,1,"single completion");
e.Apply(new(t.AddSeconds(50),CombatKind.PlayerName,2,"Player"));e.Apply(Hit(50,2,400,"Player"));Equal(e.Snapshot(MeterSegment.Previous,MeterCategory.Damage).FightDamage,300,"previous fight");
Equal(e.Snapshot(MeterSegment.Overall,MeterCategory.Damage).FightDamage,700,"overall sum");
e.Apply(new(t.AddSeconds(51),CombatKind.Heal,2,"Player",1,"Player","Heal",50));Equal(e.Snapshot(MeterSegment.Current,MeterCategory.Healing).Players.Single().Damage,50,"healing independent");
Equal(e.Snapshot(MeterSegment.Current,MeterCategory.DamageTaken).Players.Single().Damage,400,"damage taken independent");
e.Apply(new(t.AddSeconds(52),CombatKind.Death,TargetId:2));Equal(e.Snapshot(MeterSegment.Current,MeterCategory.Deaths).Players.Single().Damage,1,"death target");
e.Apply(new(t.AddSeconds(53),CombatKind.Interrupt,2));Equal(e.Snapshot(MeterSegment.Current,MeterCategory.Interrupts).Players.Single().Damage,1,"interrupt count");
e.Apply(new(t.AddSeconds(54),CombatKind.Dispel,2));Equal(e.Snapshot(MeterSegment.Current,MeterCategory.Dispels).Players.Single().Damage,1,"dispel count");
var b=new CombatEngine(()=>t.AddSeconds(7));b.Apply(Hit(0,1,100));b.Apply(new(t.AddSeconds(2),CombatKind.BuffApply,1,"Player",1,"Player",Effect:"Power",Stacks:2));b.Apply(new(t.AddSeconds(4),CombatKind.BuffRemove,1,"Player",1,"Player",Effect:"Power"));b.Apply(Hit(5,1,100));
Equal(b.Snapshot().Buffs.Single().Uptime,40,"buff actual fight denominator");
b.Apply(new(t.AddSeconds(6),CombatKind.PlayerName,1,"Resolved",SourceClass:"Cleric"));b.Apply(new(t.AddSeconds(6),CombatKind.PlayerName,2,"Player"));b.Apply(Hit(6,2,300,"Player"));Equal(b.Snapshot().Players.Count,2,"same names remain separate");Equal(b.Snapshot().Skills.Count,2,"skills per actor");
True(b.Snapshot().Players.Any(p=>p.Name=="Resolved"&&p.ClassName=="Cleric"&&p.Damage==200),"late identity");
b.Apply(new(t.AddSeconds(6),CombatKind.Despawn,1));b.Apply(new(t.AddSeconds(7),CombatKind.PlayerName,1,"Replacement"));b.Apply(Hit(7,1,50,"Replacement"));True(b.Snapshot().Players.Any(p=>p.Name=="Resolved"&&p.Damage==200),"reused ID history preserved");True(b.Snapshot().Players.Any(p=>p.Name=="Replacement"&&p.Damage==50),"reused ID new actor");
var pet=new CombatEngine(()=>t);pet.Apply(new(t,CombatKind.PlayerName,1,"Owner"));pet.Apply(new(t,CombatKind.Ownership,5,OwnerId:1));pet.Apply(Hit(0,5,90,"Summon"));True(pet.Snapshot().Players.Single().Name=="Owner","explicit pet ownership");
var bossNow=t;var bossEngine=new CombatEngine(()=>bossNow);bossEngine.Apply(new(t,CombatKind.PlayerName,1,"Player"));bossEngine.Apply(Hit(0,1,100,boss:true));bossNow=t.AddSeconds(7);True(bossEngine.Snapshot().InFight,"boss retained inside inactivity window");bossEngine.Apply(new(t.AddSeconds(7),CombatKind.TargetHp,TargetId:99,CurrentHp:0,MaxHp:1000,IsBoss:true));True(!bossEngine.Snapshot().InFight,"boss death completes");
var tcp=new TcpStreamReassembler();
Equal(tcp.Push("a",100,new byte[]{1,2,3},t).SelectMany(x=>x).Count(),3,"initial TCP");
Equal(tcp.Push("a",102,new byte[]{3,4,5},t).SelectMany(x=>x).Count(),2,"overlap keeps new suffix");
Equal(tcp.Push("a",102,new byte[]{3,4,5},t).Count,0,"duplicate discarded");
Equal(tcp.Push("a",108,new byte[]{9},t).Count,0,"out of order waits");Equal(tcp.Push("a",105,new byte[]{6,7,8},t).SelectMany(x=>x).Count(),4,"gap filled flushes");
int resets=0;tcp.StreamReset+=_=>resets++;tcp.Push("a",120,new byte[]{1},t);tcp.Push("a",121,new byte[]{2},t.AddSeconds(6));True(resets>0,"gap recovery reset");
Equal(tcp.Push("wrap",uint.MaxValue-1,new byte[]{1,2,3},t).Count,1,"sequence wrap first");Equal(tcp.Push("wrap",1,new byte[]{4},t).Count,1,"sequence wrap continuation");
var profile=new ProtocolProfile("fixture","Global","fixture",13328,new Dictionary<string,PacketTag>{{"damage",new(4,56)},{"entityRemoved",new(33,141)}});
// Observed frame regressions, with logged values independently encoded here. These are NOT a ground-truth fight total.
var observed=new[]{("210438E3A00204008D3540B7B70009020B95C34701000000D658E7020100",359L),("210438E3A00204008D3540B7B70009021595C34702000000D658E8020200",360L),("240438E3A00224008D3540B7B70009021595C34703000000D65888030203030300",392L),("280438E3A00226008D351042B7000B020800004BCE954701000000D658D50F031414140100",2005L)};
foreach(var (hex,amount) in observed)
{
 var decoder=new CurrentClientDecoder(profile);var evt=decoder.Decode(Convert.FromHexString(hex),t).Single(x=>x.Kind==CombatKind.Damage);Equal(evt.Amount,amount,"observed damage replay");Equal(evt.SourceId,6797,"observed actor");Equal(evt.TargetId,36963,"observed target");
 var split=new CurrentClientDecoder(profile);var bytes=Convert.FromHexString(hex);True(!split.DecodeStream("in",bytes[..8],t).Any(),"split frame waits");Equal(split.DecodeStream("in",bytes[8..],t).Single(x=>x.Kind==CombatKind.Damage).Amount,amount,"split frame replay");
}
var directions=new CurrentClientDecoder(profile);var f=Convert.FromHexString(observed[0].Item1);directions.DecodeStream("in",f[..8],t).ToArray();directions.DecodeStream("out",f[..5],t).ToArray();Equal(directions.DecodeStream("in",f[8..],t).Single().Amount,359,"directions isolated");
var removal=new CurrentClientDecoder(profile).Decode(Convert.FromHexString("0B218DED8A010000"),t).Single();True(removal.Kind==CombatKind.Despawn,"observed removal");Equal(removal.SourceId,17773,"observed removal ID");
Equal(Lz4BlockDecoder.Decompress(new byte[]{0,0,0},new byte[10]),-1,"bad LZ4 rejects");
var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));try {var store=new FightStore(folder);await store.SaveAsync(b.Snapshot());var saved=await store.LoadAsync(store.List().Single());Equal(saved!.FightDamage,b.Snapshot().FightDamage,"history roundtrip");Equal(saved.Skills.Count,b.Snapshot().Skills.Count,"history skill roundtrip");}finally{Directory.Delete(folder,true);}
var reuseRow=b.Snapshot().Players.Single(p=>p.Name=="Replacement");Equal(b.Snapshot().RecentEvents.Where(x=>x.Kind==CombatKind.Damage&&x.SourceId==reuseRow.ActorId).Sum(x=>x.Amount),50,"reused actor report events isolated");
var savedSnapshot=e.Snapshot();e.ResetFight();True(e.History.Last().Categories[MeterCategory.Healing].Players.Count>0,"completed history retains healing dataset");
// Sanitized identity prefixes observed in the new log: no private names or inventory data.
byte[] Frame(string bodyHex)
{
 var body=Convert.FromHexString(bodyHex);var length=new List<byte>();uint v=(uint)body.Length+4;
 do {byte part=(byte)(v&127);v>>=7;length.Add((byte)(part|(v>0?128:0)));}while(v>0);
 return length.Concat(body).ToArray();
}
// Research candidates are recorded as evidence only, never as score/spawn facts.
var researchDispatcher=new PacketDispatcher(profile);
var researchRecords=new List<string>();researchDispatcher.ValidationRecord+=researchRecords.Add;
for(int sample=0;sample<20;sample++)
{
 True(!researchDispatcher.Dispatch(Frame("0191"+new string('0',32)),t).Any(),"boss-list candidate creates no combat events");
 True(!researchDispatcher.Dispatch(Frame("0297"+new string('0',32)),t).Any(),"roster candidate creates no combat events");
}
Equal(researchRecords.Count(x=>x.Contains("tag=featureCandidate")),24,"research logging capped at 12 samples per family");
True(researchRecords.Where(x=>x.Contains("tag=featureCandidate")).All(x=>x.Contains("status=unverified")&&x.Contains("raw=")),"research samples preserve evidence and uncertainty");
var identityProfile=profile with {Tags=new Dictionary<string,PacketTag>(profile.Tags) {{"selfInfo",new(51,54)},{"otherInfo",new(69,54)}}};
var identityDecoder=new CurrentClientDecoder(identityProfile);
var self=identityDecoder.Decode(Frame("3336FD235F81C1283708546573744865726F000000"),t).Single(x=>x.Kind==CombatKind.PlayerName);
True(self.Source=="TestHero"&&self.SourceId==4605,"observed self identity prefix");
// Sanitized replay of selfInfo -> local entity removal -> damage in the 13:00 capture.
var localDecoder=new CurrentClientDecoder(identityProfile);
localDecoder.Decode(Frame("3336D10B5F81C1283708546573744865726F000000"),t).ToArray();
True(!localDecoder.Decode(Convert.FromHexString("0A218DD10B0001"),t.AddSeconds(3)).Any(x=>x.Kind==CombatKind.Despawn),"local removal must not invalidate self identity");
var localHit=localDecoder.Decode(Convert.FromHexString("230438E3A0022400D10B40B7B70001020B95C34701000000D658F10201030100"),t.AddSeconds(3)).Single(x=>x.Kind==CombatKind.Damage);
True(localHit.SourceId==1489&&localHit.Source=="TestHero","captured local removal sequence retains character name");
True(localDecoder.Decode(Convert.FromHexString("0B218DED8A010000"),t.AddSeconds(4)).Any(x=>x.Kind==CombatKind.Despawn),"other entities still despawn");
localDecoder.ResetConnection();
True(localDecoder.Decode(Convert.FromHexString("0A218DD10B0001"),t.AddSeconds(5)).Any(x=>x.Kind==CombatKind.Despawn),"reset clears protected self identity");
var ally=identityDecoder.Decode(Frame("4536A2210120A401070854657374416C6C7913000000"),t).Last(x=>x.Kind==CombatKind.PlayerName);
True(ally.Source=="TestAlly"&&ally.SourceId==4258,"observed other identity prefix");
True(!identityDecoder.Decode(Frame("218DA2210001"),t.AddSeconds(1)).Any(x=>x.Kind==CombatKind.Despawn),"known ally visibility removal preserves identity");
var allyDamage=identityDecoder.Decode(Convert.FromHexString("210438E3A0020400A22140B7B70009020B95C34701000000D658E7020100"),t.AddSeconds(2)).Single(x=>x.Kind==CombatKind.Damage);
True(allyDamage.Source=="TestAlly","ally name remains after visibility removal");
var nameReuse=new CombatEngine(()=>t);
nameReuse.Apply(new(t,CombatKind.PlayerName,4258,"FirstPlayer"));
nameReuse.Apply(Hit(0,4258,100,"FirstPlayer"));
nameReuse.Apply(new(t.AddSeconds(1),CombatKind.PlayerName,4258,"SecondPlayer"));
nameReuse.Apply(Hit(2,4258,50,"SecondPlayer"));
True(nameReuse.Snapshot().Players.Any(p=>p.Name=="FirstPlayer"&&p.Damage==100),"explicit name reuse preserves old totals");
True(nameReuse.Snapshot().Players.Any(p=>p.Name=="SecondPlayer"&&p.Damage==50),"explicit name reuse separates new totals");
var bridge=new Aion2DPSPro.Capture.CaptureIdentityBridge();string scope="adapter|local|server";
bridge.Observe(scope,new(t,CombatKind.PlayerName,self.SourceId,self.Source));bridge.Observe(scope,new(t,CombatKind.PlayerName,ally.SourceId,ally.Source));
var bridged=new CombatEngine(()=>t);
foreach(var identity in bridge.Identities(scope,t))bridged.Apply(identity);
bridged.Apply(bridge.Resolve(scope,Hit(0,4605,100,"Actor 4605")));bridged.Apply(bridge.Resolve(scope,Hit(0,4258,50,"Actor 4258")));
True(bridged.Snapshot().Players.Any(p=>p.Name=="TestHero"&&p.EntityId==4605),"pre-lock self identity reaches combat rows");
True(bridged.Snapshot().Players.Any(p=>p.Name=="TestAlly"&&p.EntityId==4258),"sibling identity reaches exact actor");
True(bridge.Resolve(scope,Hit(0,4605,100,"FreshName")).Source=="FreshName","bridge preserves explicitly decoded names");
True(bridge.Resolve("adapter|local|different-server",Hit(0,4605,100,"Actor 4605")).Source=="Actor 4605","identity scope isolation");
bridge.Observe(scope,new(t,CombatKind.Despawn,4605));True(bridge.Resolve(scope,Hit(0,4605,100,"Actor 4605")).Source=="Actor 4605","bridge removes stale identity");
True(bridge.Identities(scope,t.AddMinutes(11)).Count==0,"identity cache expires");
var reportEngine=new CombatEngine(()=>t);reportEngine.Apply(new(t,CombatKind.PlayerName,1,"Tester",SourceClass:"Templar"));reportEngine.Apply(new(t,CombatKind.Damage,1,"Tester",2,"Dummy","One",100,DamageFlags:DamageFlags.Critical|DamageFlags.Back|DamageFlags.Perfect));reportEngine.Apply(new(t.AddSeconds(1),CombatKind.Heal,1,"Tester",1,"Tester","Mend",50));
var full=reportEngine.Snapshot(MeterSegment.Current,MeterCategory.Damage,true);Equal(full.Categories[MeterCategory.Healing].Skills.Single().Damage,50,"report retains healing skills");Equal(full.Skills.Single().FlagHits[DamageFlags.Perfect],1,"perfect counts");Equal(full.Targets.Single().Damage,100,"actor target totals");
reportEngine.ResetFight();reportEngine.Apply(Hit(10,1,999));Equal(reportEngine.SnapshotEncounter(full.EncounterId)!.FightDamage,100,"open report stays on its encounter");
True(PublicGameData.EnglishTargetLabel("Training Scarecrow")=="Training Scarecrow","English target label preserved");
True(PublicGameData.EnglishTargetLabel("노인 주민")==null,"untranslated Korean NPC falls back to English target label");
True(PublicGameData.EnglishTargetLabel("Boss 마족")==null,"mixed language target label rejected");
// Synthetic roster fixture built from the published layout, separate from live damage evidence.
byte[] ScoreRoster(uint stable=304076,ushort server=1102,uint gear=2800,ulong power=121600,string name="TestHero")
{
 var data=new List<byte>();data.AddRange(BitConverter.GetBytes(123u));data.Add(0);data.Add(5);data.AddRange(new byte[4+2]);data.AddRange(BitConverter.GetBytes(((ulong)server<<48)|stable));data.AddRange(new byte[3]);data.Add(2);
 data.Add(0x2C);data.Add(1);data.AddRange(BitConverter.GetBytes(((ulong)server<<48)|stable));var text=System.Text.Encoding.UTF8.GetBytes(name);data.Add((byte)text.Length);data.AddRange(text);
 data.AddRange(BitConverter.GetBytes(9u));data.AddRange(BitConverter.GetBytes(45u));data.AddRange(BitConverter.GetBytes(gear));data.Add(1);data.AddRange(BitConverter.GetBytes(server));data.AddRange(BitConverter.GetBytes(server));data.Add(0);data.AddRange(BitConverter.GetBytes(power));data.AddRange(new byte[3+11]);return data.ToArray();
}
var scoreBody=ScoreRoster();var parsedScores=PartyRosterScores.Parse(scoreBody,t);Equal(parsedScores.Count,1,"strict roster reads score record");Equal(parsedScores.Single().GearScore!.Value,2800,"GS differs from CP");Equal(parsedScores.Single().CombatPower!.Value,121600,"roster CP");
True(!PartyRosterScores.Parse(ScoreRoster(gear:999999),t).Any(),"invalid gear rejected");True(!PartyRosterScores.Parse(ScoreRoster(power:ulong.MaxValue),t).Any(),"invalid CP rejected");
for(int length=0;length<scoreBody.Length-14;length++)True(!PartyRosterScores.Parse(scoreBody.AsSpan(0,length),t).Any(),"truncated roster rejected");
var scoreProfile=profile with {ExperimentalRosterScores=true};var scoreDispatch=new PacketDispatcher(scoreProfile);
byte[] AsFrame(byte[] body)=>Frame(Convert.ToHexString(body));
scoreDispatch.Dispatch(AsFrame(new byte[]{2,0x97}.Concat(scoreBody).ToArray()),t).ToArray();
var linkEvents=scoreDispatch.Dispatch(Convert.FromHexString("CF0B20360000D01E00000000CCA3040000003608A602CC08E615"),t).ToArray();
var scoreIdentity=scoreDispatch.AttachRosterScores(linkEvents.Single(x=>x.Kind==CombatKind.PlayerName),t);
Equal(scoreIdentity.SourceId,3920,"roster bound through explicit session link");Equal(scoreIdentity.SourceGearScore!.Value,2800,"linked GS");Equal(scoreIdentity.SourceCombatPower!.Value,121600,"linked CP");Equal(scoreIdentity.SourceServerId,1102,"linked server");
True(scoreDispatch.AttachRosterScores(scoreIdentity with {SourceGearScore=null,SourceCombatPower=null,StatsObservedUtc=null},t.AddMinutes(11)).SourceGearScore==null,"expired roster does not reattach");
scoreDispatch.Dispatch(AsFrame(new byte[]{2,0x97}.Concat(ScoreRoster(server:1202)).ToArray()),t).ToArray();
True(scoreDispatch.AttachRosterScores(scoreIdentity with {SourceGearScore=null,SourceCombatPower=null,StatsObservedUtc=null},t).SourceGearScore==null,"same character id on two servers stays unknown");
var scoreClock=t;var scoreEngine=new CombatEngine(()=>scoreClock);
scoreEngine.Apply(new(t,CombatKind.PlayerName,3920,"TestHero",SourceClass:"Templar",SourceServerId:1102,SourceCharacterId:304076,SourceGearScore:2800,SourceCombatPower:121600,StatsObservedUtc:t));
scoreEngine.Apply(Hit(0,3920,100,"TestHero"));Equal(scoreEngine.Snapshot().Players.Single().GearScore!.Value,2800,"engine GS");Equal(scoreEngine.Snapshot().FightDamage,100,"metadata does not alter damage");
scoreEngine.Apply(new(t,CombatKind.PlayerName,3920,"TestHero",SourceClass:"Templar",SourceServerId:1102,SourceCharacterId:304076,SourceGearScore:2700,SourceCombatPower:90000,StatsObservedUtc:t.AddSeconds(-1)));
Equal(scoreEngine.Snapshot().Players.Single().CombatPower!.Value,121600,"older stats cannot overwrite newer");
scoreEngine.ResetFight();scoreClock=t.AddHours(1);Equal(scoreEngine.Snapshot(MeterSegment.Previous,MeterCategory.Damage).Players.Single().GearScore!.Value,2800,"archived scores stay frozen");
True(scoreEngine.Snapshot(MeterSegment.Overall,MeterCategory.Damage).Players.Single().GearScore==null,"stale live overall scores expire");
scoreEngine.Apply(new(scoreClock,CombatKind.Despawn,3920));scoreEngine.Apply(new(scoreClock,CombatKind.PlayerName,3920,"Replacement",SourceClass:"Cleric"));scoreEngine.Apply(new(scoreClock,CombatKind.Damage,3920,"Replacement",99,"Boss","Strike",300));True(scoreEngine.Snapshot().Players.Single().GearScore==null,"reused actor cannot inherit old score");
var targetEngine=new CombatEngine(()=>t);targetEngine.Apply(new(t,CombatKind.PlayerName,1,"One"));targetEngine.Apply(new(t,CombatKind.PlayerName,2,"Two"));targetEngine.Apply(new(t,CombatKind.Damage,1,"One",90,"Add","Strike",1000));targetEngine.Apply(new(t,CombatKind.Damage,1,"One",99,"Boss","Strike",100));targetEngine.Apply(new(t,CombatKind.Damage,2,"Two",99,"Boss","Strike",300));var targetView=targetEngine.Snapshot();Equal(targetView.Targets.Where(x=>x.TargetId==targetView.SelectedTargetId).Sum(x=>x.Damage),400,"target share excludes add damage");
var gearBridge=new Aion2DPSPro.Capture.CaptureIdentityBridge();string gearScope="identity-socket|local|game";
var rosterMeta=new CombatEvent(t,CombatKind.CharacterStats,Source:"TestHero",SourceServerId:1102,SourceCharacterId:304076,SourceGearScore:2800,SourceCombatPower:121600,StatsObservedUtc:t);
gearBridge.Observe(gearScope,rosterMeta);
var linkedName=new CombatEvent(t,CombatKind.PlayerName,3920,"TestHero",SourceClass:"Templar",SourceServerId:1102,SourceCharacterId:304076);
gearBridge.Observe(gearScope,linkedName);var bridgedScore=gearBridge.Identities(gearScope,t).Single();Equal(bridgedScore.SourceGearScore!.Value,2800,"roster scores reach exact linked identity");
True(gearBridge.Resolve("other-server|local|other",linkedName).SourceGearScore==null,"profile bridge isolates endpoint scope");
True(gearBridge.Resolve(gearScope,linkedName with {SourceServerId=1202}).SourceGearScore==null,"profile bridge requires exact server");
True(gearBridge.Resolve(gearScope,linkedName with {SourceCharacterId=304077}).SourceGearScore==null,"profile bridge requires exact character");
True(gearBridge.Resolve(gearScope,linkedName with {SourceServerId=0}).SourceGearScore==null,"profile bridge refuses unknown server");
var lateGearBridge=new Aion2DPSPro.Capture.CaptureIdentityBridge();lateGearBridge.Observe(gearScope,linkedName);lateGearBridge.Observe(gearScope,rosterMeta);Equal(lateGearBridge.Identities(gearScope,t).Single().SourceCombatPower!.Value,121600,"late roster updates cached identity");
var identityScope="\\Device\\NPF_TEST|local|game";var socketScope="\\Device\\NPF_OTHER|local|game";var socketBridge=new Aion2DPSPro.Capture.CaptureIdentityBridge();socketBridge.Observe(identityScope,rosterMeta);socketBridge.Observe(socketScope,linkedName);Equal(socketBridge.Identities(socketScope,t).Single().SourceGearScore!.Value,2800,"score bridge shares duplicate adapter/endpoint scope");
var copyScore=Aion2DPSPro.Sharing.FightSummary.Format(scoreEngine.Snapshot(MeterSegment.Previous,MeterCategory.Damage));True(copyScore.Contains("GS 2800 / CP 121.6K"),"summary includes distinct GS/CP");
var copyPrivate=Aion2DPSPro.Sharing.FightSummary.Format(scoreEngine.Snapshot(MeterSegment.Previous,MeterCategory.Damage),true,false);True(!copyPrivate.Contains("TestHero")&&!copyPrivate.Contains("GS"),"summary respects privacy and score toggles");
True(Aion2DPSPro.Sharing.FightSummary.Format(scoreEngine.Snapshot() with {PreviewMode=true}).Contains("SAMPLE DATA"),"sample summary remains labeled");
var timerProfile=new Aion2DPSPro.Schedules.ScheduleProfile("test","UTC test","UTC","test fixture","2026-10-14",new[]{new Aion2DPSPro.Schedules.ScheduleEvent("rift","Spacetime Rift",new[]{0,3,6,9,12,15,18,21},0,10)});
var timerNow=new DateTimeOffset(2026,10,14,2,59,59,TimeSpan.Zero);
var nextRift=Aion2DPSPro.Schedules.EventSchedule.Evaluate(timerProfile,timerNow).Single();
Equal(nextRift.Remaining(timerNow).TotalSeconds,1,"rift countdown to next opening");
var openRift=Aion2DPSPro.Schedules.EventSchedule.Evaluate(timerProfile,timerNow.AddSeconds(1)).Single();
True(openRift.InEntryWindow,"scheduled entry begins at boundary");Equal(openRift.Remaining(timerNow.AddSeconds(1)).TotalMinutes,10,"rift entry duration");
True(!Aion2DPSPro.Schedules.EventSchedule.Evaluate(timerProfile,new DateTimeOffset(2026,10,14,3,10,0,TimeSpan.Zero)).Single().InEntryWindow,"entry end is exclusive");
Equal(Aion2DPSPro.Schedules.EventSchedule.Evaluate(timerProfile,new DateTimeOffset(2026,10,14,23,59,0,TimeSpan.Zero)).Single().Start.Hour,0,"rift rolls into tomorrow");
var dst=timerProfile with {TimeZoneId="America/New_York",Events=new[]{new Aion2DPSPro.Schedules.ScheduleEvent("dst","DST test",new[]{2},30,10)}};
var afterGap=Aion2DPSPro.Schedules.EventSchedule.Evaluate(dst,new DateTimeOffset(2026,3,8,6,59,0,TimeSpan.Zero)).Single();
Equal(afterGap.Start.Day,9,"nonexistent DST opening skipped");
var repeated=dst with {Events=new[]{new Aion2DPSPro.Schedules.ScheduleEvent("dst","DST test",new[]{1},30,60)}};
var fallNow=new DateTimeOffset(2026,11,1,6,45,0,TimeSpan.Zero);
var fall=Aion2DPSPro.Schedules.EventSchedule.Evaluate(repeated,fallNow).Single();True(fall.InEntryWindow,"fall DST repeated opening supported");Equal(fall.Start.Hour,6,"fall uses latest active occurrence");
var imported=Aion2DPSPro.Schedules.EventSchedule.Load("src/Schedules/global-community.json");Equal(imported.Length,3,"three source-labeled schedule candidates import");
var badSchedule=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");try{
 File.WriteAllText(badSchedule,System.Text.Json.JsonSerializer.Serialize(new[]{timerProfile with {Events=new[]{new Aion2DPSPro.Schedules.ScheduleEvent("bad","Bad",new[]{24},0,10)}}}));
 bool rejected=false;try{Aion2DPSPro.Schedules.EventSchedule.Load(badSchedule);}catch(InvalidDataException){rejected=true;}True(rejected,"invalid imported hour rejected");
 File.WriteAllText(badSchedule,"[null]");rejected=false;try{Aion2DPSPro.Schedules.EventSchedule.Load(badSchedule);}catch(InvalidDataException){rejected=true;}True(rejected,"null imported profile rejected");
}finally{File.Delete(badSchedule);}
Console.WriteLine($"PASS: {checks} regression assertions");
// Export privacy and unsupported dataset regressions.
var exportEngine=new CombatEngine(()=>t.AddSeconds(10));
exportEngine.Apply(new(t,CombatKind.PlayerName,909,"SecretPlayerName",SourceClass:"Templar",SourceServerId:1102,SourceCharacterId:123456,SourceGearScore:2900,SourceCombatPower:120000,StatsObservedUtc:t));
exportEngine.Apply(new(t,CombatKind.Damage,909,"SecretPlayerName",101,"PrivateTargetName","SecretSkill",500));
exportEngine.Apply(new(t.AddSeconds(5),CombatKind.Damage,909,"SecretPlayerName",101,"PrivateTargetName","SecretSkill",500));
bool ExportRejected(MeterSnapshot snapshot){try{Aion2DPSPro.Sharing.FightLogExport.Serialize(snapshot);return false;}catch(InvalidOperationException){return true;}}
True(ExportRejected(exportEngine.Snapshot()),"in-progress export rejected");exportEngine.ResetFight();var exported=exportEngine.Snapshot(MeterSegment.Previous,MeterCategory.Damage,true);
var uploadJson=Aion2DPSPro.Sharing.FightLogExport.Serialize(exported);
True(uploadJson.Contains("SecretPlayerName")&&!uploadJson.Contains("PrivateTargetName")&&!uploadJson.Contains("SecretSkill")&&!uploadJson.Contains("123456"),"export keeps character names but strips target, stable IDs and raw events");
using(var namedDoc=System.Text.Json.JsonDocument.Parse(uploadJson)){Equal(namedDoc.RootElement.GetProperty("players")[0].GetProperty("serverId").GetInt32(),1102,"export preserves known server");}
using(var doc=System.Text.Json.JsonDocument.Parse(uploadJson)){Equal(doc.RootElement.GetProperty("players")[0].GetProperty("damage").GetInt64(),1000L,"export preserves damage");True(doc.RootElement.GetProperty("visibility").GetString()=="private","export private default");}
True(ExportRejected(exported with {PreviewMode=true}),"sample export rejected");True(ExportRejected(exported with {EncounterId=Guid.Empty}),"overall export rejected");
True(ExportRejected(exported with {FightSeconds=double.NaN}),"non-finite duration rejected");
Console.WriteLine($"PASS: {checks} regression assertions including export privacy");
