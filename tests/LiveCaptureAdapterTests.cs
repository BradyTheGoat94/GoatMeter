#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aion2DPSPro.Capture;
using Aion2DPSPro.Protocol;
using PacketDotNet;
using SharpPcap;
using Xunit;

namespace Aion2DPSPro.Tests;

public sealed class LiveCaptureAdapterTests
{
    // Exercise the real processing worker without requiring Npcap or live traffic.
    private static BlockingCollection<Action> Queue(LiveCaptureAdapter adapter) =>
        (BlockingCollection<Action>)typeof(LiveCaptureAdapter)
            .GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;

    private static void Packet(LiveCaptureAdapter adapter, uint sequence, DateTime utc, ushort port = 40000)
    {
        var tcp = new TcpPacket(13328, port) { SequenceNumber = sequence, PayloadData = new byte[] { 1 } };
        var ip = new IPv4Packet(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.2")) { PayloadPacket = tcp };
        typeof(LiveCaptureAdapter).GetMethod("Process", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(adapter, new object[] { "test", ip, utc });
    }

    private sealed class Decoder : IAion2Decoder
    {
        public string ProfileId => "test";
        public IEnumerable<Aion2Decoded> Decode(byte[] payload, DateTime utc)
        {
            yield return Event(CombatKind.PlayerName, 0);
            yield return Event(CombatKind.Damage, 123);
            yield return Event(CombatKind.Heal, 45);
            yield return Event(CombatKind.Damage, 789);
        }
        private static Aion2Decoded Event(CombatKind kind, long amount) =>
            new(kind, 7, "Hero", 99, "Boss", "Strike", amount, DamageType.Crit, 0, 0, "", 0);
    }

    [Fact]
    public async Task SiblingIdentityNotificationsDoNotCountSiblingCombatTwice()
    {
        var now=DateTime.UnixEpoch;
        using var adapter=new LiveCaptureAdapter(new Decoder());
        var engine=new CombatEngine(()=>now.AddSeconds(2));
        var events=new List<CombatEvent>();
        adapter.EventReceived += _=>throw new Exception("listener");
        adapter.EventReceived += e=> {engine.Apply(e);events.Add(e);};
        Queue(adapter).Add(()=>Packet(adapter,1,now));
        Queue(adapter).Add(()=>Packet(adapter,1,now.AddMilliseconds(500),40001));
        Queue(adapter).Add(()=>Packet(adapter,2,now.AddSeconds(1)));
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1824,engine.Snapshot().FightDamage);
        Assert.Equal(4,events.Count(e=>e.Kind==CombatKind.Damage));
        Assert.Equal(4,events.Count(e=>e.Kind==CombatKind.PlayerName));
        Assert.Equal(10,events.Count);
    }

    [Fact]
    public async Task FailedQueueActionDoesNotPreventFollowingPacketOrDrain()
    {
        using var adapter=new LiveCaptureAdapter(new Decoder());
        var events=new List<CombatEvent>();
        var statuses=new List<string>();
        adapter.EventReceived += events.Add;
        adapter.StatusChanged += _=>throw new Exception("status");
        adapter.StatusChanged += statuses.Add;
        Queue(adapter).Add(()=>throw new InvalidOperationException("bad packet"));
        Queue(adapter).Add(()=>Packet(adapter,1,DateTime.UnixEpoch));
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(912,events.Where(e=>e.Kind==CombatKind.Damage).Sum(e=>e.Amount));
        Assert.Single(statuses,s=>s=="Capture/decoder error: bad packet");
        Assert.True(adapter.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task QueuedPacketBurstWithBrokenListenerDrainsExactlyOnceAndKeepsRecentHistoryBounded()
    {
        const int packets=2500;
        var now=DateTime.UnixEpoch;
        using var adapter=new LiveCaptureAdapter(new Decoder());
        var engine=new CombatEngine(()=>now.AddSeconds(4));
        var delivered=0;
        var failures=0;
        adapter.EventReceived += _=>throw new Exception("listener");
        adapter.EventReceived += e=> {engine.Apply(e);delivered++;};
        adapter.StatusChanged += s=> {if(s.StartsWith("Combat event notification failed:"))failures++;};
        for(var i=0;i<packets;i++)
        {
            var sequence=(uint)(i+1);
            var utc=now.AddMilliseconds(i);
            Queue(adapter).Add(()=>Packet(adapter,sequence,utc));
        }
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        var snapshot=engine.Snapshot();
        Assert.Equal(packets*912L,snapshot.FightDamage);
        Assert.Equal(packets*45L,engine.Snapshot(MeterSegment.Current,MeterCategory.Healing).Players.Single().Damage);
        Assert.Equal(packets*4+1,delivered); // one initial identity replay
        Assert.Equal(delivered,failures);
        Assert.Equal(2000,snapshot.RecentEvents.Count);
        Assert.True(adapter.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ThrowingListenersPreserveBatchOrderInstancesAndCombatTotals()
    {
        using var adapter = new LiveCaptureAdapter(new Decoder());
        var actual = new List<CombatEvent>();
        var first = new List<CombatEvent>();
        var statuses = new List<string>();
        var now = DateTime.UnixEpoch;
        var engine = new CombatEngine(() => now.AddSeconds(2));
        adapter.StatusChanged += _ => throw new Exception("status");
        adapter.StatusChanged += statuses.Add;
        adapter.ValidationRecord += _ => throw new Exception("validation");
        var validations = 0;
        adapter.ValidationRecord += _ => validations++;
        adapter.FlowLocked += _ => throw new Exception("flow");
        var locks = 0;
        adapter.FlowLocked += _ => locks++;
        adapter.EventReceived += e => { first.Add(e); throw new Exception("combat"); };
        adapter.EventReceived += e => { engine.Apply(e); actual.Add(e); };
        // An exception after application must never cause an event retry.
        adapter.EventReceived += _ => throw new Exception("after application");
        Queue(adapter).Add(() => Packet(adapter, 1, now));
        Queue(adapter).Add(() => Packet(adapter, 2, now.AddSeconds(1)));
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, locks);
        Assert.Equal(8, validations); // identity plus three combat records per packet
        Assert.Equal(9, actual.Count); // pre-lock identity replay plus two complete batches
        Assert.Equal(first, actual);
        for (var i = 0; i < first.Count; i++) Assert.Same(first[i], actual[i]);
        Assert.Equal(new long[] { 123, 45, 789, 123, 45, 789 },
            actual.Where(e => e.Kind is CombatKind.Damage or CombatKind.Heal).Select(e => e.Amount));
        var baseline = new CombatEngine(() => now.AddSeconds(2));
        foreach (var e in actual) baseline.Apply(e);
        Assert.Equal(1824, engine.Snapshot().FightDamage);
        Assert.Equal(baseline.Snapshot().FightDps, engine.Snapshot().FightDps);
        Assert.Contains(statuses, s => s.StartsWith("Combat event notification failed:"));
        Assert.Contains(statuses, s => s.StartsWith("Validation notification failed:"));
        Assert.Contains(statuses, s => s.StartsWith("Flow lock notification failed:"));
        Assert.DoesNotContain(statuses, s => s.StartsWith("Capture/decoder error:"));
    }

    [Fact]
    public async Task DisposeFromWorkerDrainsAcceptedWorkAndIsIdempotent()
    {
        using var adapter = new LiveCaptureAdapter(new Decoder());
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var processed = 0;
        Queue(adapter).Add(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            adapter.Dispose();
            Interlocked.Increment(ref processed);
        });
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Queue(adapter).Add(() => Interlocked.Increment(ref processed));
            Assert.False(adapter.Completion.IsCompleted);
        }
        finally { release.Set(); }
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        adapter.Dispose();
        adapter.Start(); // disposed adapters must not reopen native devices
        Assert.Equal(2, processed);
        Assert.True(Queue(adapter).IsAddingCompleted);
        Assert.Throws<InvalidOperationException>(() => Queue(adapter).TryAdd(() => { }));
    }

    [Fact]
    public async Task FailedDeviceTeardownStillClosesEveryDeviceAndDrainsWorker()
    {
        using var adapter = new LiveCaptureAdapter(new Decoder());
        var calls = new List<string>();
        var devices = (List<ICaptureDevice>)typeof(LiveCaptureAdapter)
            .GetField("devices", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
        for (var i = 0; i < 2; i++)
        {
            var device = DispatchProxy.Create<ICaptureDevice, FailingDevice>();
            ((FailingDevice)(object)device).Calls = calls;
            devices.Add(device);
        }
        adapter.StatusChanged += _ => throw new Exception("status");
        var statuses = new List<string>();
        adapter.StatusChanged += statuses.Add;
        var drained = false;
        Queue(adapter).Add(() => drained = true);
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        adapter.Dispose();
        Assert.Equal(new[] { "remove_OnPacketArrival", "StopCapture", "Close",
            "remove_OnPacketArrival", "StopCapture", "Close" }, calls);
        Assert.Equal(6, statuses.Count);
        Assert.True(drained);
        Assert.Empty(devices);
    }

    public class FailingDevice : DispatchProxy
    {
        public List<string> Calls = new();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod!.Name);
            throw new InvalidOperationException("disconnected adapter");
        }
    }

    public class StartupDevice : DispatchProxy
    {
        public List<string> Calls=new();
        public PacketArrivalEventHandler? Arrival;
        public string? FailOn;
        public Action? OnStart;
        public Action? OnStop;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name=targetMethod!.Name;
            Calls.Add(name);
            if(name==FailOn)throw new InvalidOperationException("startup failed");
            if(name=="add_OnPacketArrival")Arrival+=(PacketArrivalEventHandler)args![0]!;
            if(name=="remove_OnPacketArrival")Arrival-=(PacketArrivalEventHandler)args![0]!;
            if(name=="StartCapture")OnStart?.Invoke();
            if(name=="StopCapture")OnStop?.Invoke();
            if(name=="get_LinkType")return LinkLayers.Raw;
            if(name=="get_Name")return "test";
            return null;
        }
        public void Emit(ICaptureDevice device,byte[] bytes,bool validHeader=true)
        {
            ICaptureHeader? header=validHeader?new SharpPcap.LibPcap.PcapHeader(0,0,(uint)bytes.Length,(uint)bytes.Length):null;
            Arrival?.Invoke(device,new PacketCapture(device,header!,bytes));
        }
    }

    private static byte[] RawPacket(uint sequence,byte[]? payload=null)
    {
        var tcp=new TcpPacket(13328,40000) {SequenceNumber=sequence,PayloadData=payload??new byte[]{1}};
        var ip=new IPv4Packet(IPAddress.Parse("10.0.0.1"),IPAddress.Parse("10.0.0.2")) {PayloadPacket=tcp};
        ip.UpdateCalculatedValues();
        return ip.Bytes;
    }

    [Fact]
    public async Task CapturedFramesReachEngineExactlyOnceThroughNativeCallbackDespiteBrokenListeners()
    {
        // Reviewed captured frame: actor 6797, target 36963, 359 damage.
        var frame=Convert.FromHexString("210438E3A00204008D3540B7B70009020B95C34701000000D658E7020100");
        var profile=new ProtocolProfile("integration","Global","fixture",13328,
            new Dictionary<string,PacketTag>{{"damage",new(4,56)}});
        var decoder=new CurrentClientDecoder(profile);
        decoder.Diagnostic += _=>throw new Exception("decoder diagnostic");
        decoder.ValidationRecord += _=>throw new Exception("decoder validation");
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var proxy=(StartupDevice)(object)device;
        using var adapter=new LiveCaptureAdapter(decoder,()=>new[]{device});
        using var entered=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        var engine=new CombatEngine();
        engine.Apply(new(DateTime.UtcNow,CombatKind.PlayerName,6797,"Hero",SourceClass:"Templar"));
        var statuses=new List<string>();
        var packets=0;
        var duplicates=0;
        adapter.StatusChanged += _=>throw new Exception("status");
        adapter.StatusChanged += statuses.Add;
        adapter.PacketCaptured += ()=>throw new Exception("packet diagnostic");
        adapter.PacketCaptured += ()=>packets++;
        adapter.DuplicateSuppressed += ()=>throw new Exception("duplicate diagnostic");
        adapter.DuplicateSuppressed += ()=>duplicates++;
        adapter.FlowLocked += _=>throw new Exception("flow diagnostic");
        adapter.ValidationRecord += _=>throw new Exception("capture validation");
        adapter.EventReceived += _=>throw new Exception("combat listener");
        adapter.EventReceived += engine.Apply;
        Queue(adapter).Add(()=> {entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException();});
        adapter.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            var batch=frame.Concat(frame).ToArray();
            var reused=RawPacket(1,batch);
            proxy.Emit(device,reused);
            Array.Fill(reused,(byte)0);
            proxy.Emit(device,RawPacket(1,batch)); // TCP duplicate must not count again.
            proxy.Emit(device,RawPacket(1+(uint)batch.Length,frame));
        }
        finally {release.Set();}
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        var snapshot=engine.Snapshot();
        Assert.Equal(1077,snapshot.FightDamage);
        var hero=Assert.Single(snapshot.Players);
        Assert.Equal("Hero",hero.Name);
        Assert.Equal(1077,hero.Damage);
        Assert.Equal(3,hero.Hits);
        Assert.Equal(3,packets);
        Assert.Equal(1,duplicates);
        Assert.DoesNotContain(statuses,s=>s.StartsWith("Capture/decoder error:"));
    }

    [Fact]
    public async Task NativeCallbackOwnsQueuedBytesBeforeCaptureBufferIsReused()
    {
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var proxy=(StartupDevice)(object)device;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
        using var entered=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        var events=new List<CombatEvent>();
        adapter.EventReceived += events.Add;
        Queue(adapter).Add(()=> {entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException();});
        adapter.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            var bytes=RawPacket(1);
            proxy.Emit(device,bytes);
            Assert.Single(Queue(adapter));
            Array.Fill(bytes,(byte)0); // Native packet memory may be reused immediately.
        }
        finally {release.Set();}
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(912,events.Where(e=>e.Kind==CombatKind.Damage).Sum(e=>e.Amount));
    }

    [Fact]
    public async Task FullWorkerQueueDropsAndReportsNativePacketWithoutBlockingCapture()
    {
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var proxy=(StartupDevice)(object)device;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
        using var entered=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        var events=new List<CombatEvent>();
        adapter.EventReceived += events.Add;
        Queue(adapter).Add(()=> {entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException();});
        adapter.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            for(var i=0;i<4096;i++)Queue(adapter).Add(()=>{});
            proxy.Emit(device,RawPacket(1));
            Assert.Contains("1 packets dropped",adapter.Health);
            Assert.Equal(4096,Queue(adapter).Count);
        }
        finally {release.Set();}
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(events);
    }

    [Fact]
    public async Task CaptureRecoversAfterQueueOverloadButRetainsAccuracyWarning()
    {
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var proxy=(StartupDevice)(object)device;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
        using var entered=new ManualResetEventSlim();
        using var release=new ManualResetEventSlim();
        using var drained=new ManualResetEventSlim();
        var events=new List<CombatEvent>();
        adapter.EventReceived += events.Add;
        Queue(adapter).Add(()=> {entered.Set();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException();});
        adapter.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            for(var i=0;i<4096;i++)Queue(adapter).Add(()=>{});
            proxy.Emit(device,RawPacket(1));
            Assert.Contains("1 packets dropped",adapter.Health);
        }
        finally {release.Set();}
        // A barrier guarantees that all queued no-op actions have drained.
        Queue(adapter).Add(()=>drained.Set());
        Assert.True(drained.Wait(TimeSpan.FromSeconds(20)));
        proxy.Emit(device,RawPacket(2));
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(912,events.Where(e=>e.Kind==CombatKind.Damage).Sum(e=>e.Amount));
        Assert.Contains("1 packets dropped",adapter.Health);
    }

    [Fact]
    public async Task MalformedNativeCallbackAndBrokenStatusListenerDoNotEscapeOrStopLaterPackets()
    {
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var proxy=(StartupDevice)(object)device;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
        var statuses=new List<string>();
        var events=new List<CombatEvent>();
        adapter.StatusChanged += _=>throw new Exception("status");
        adapter.StatusChanged += statuses.Add;
        adapter.EventReceived += events.Add;
        adapter.Start();
        proxy.Emit(device,RawPacket(1),false);
        proxy.Emit(device,RawPacket(1));
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(statuses,s=>s.StartsWith("Capture callback error; data may be incomplete:"));
        Assert.Contains("1 packets dropped",adapter.Health);
        Assert.Equal(912,events.Where(e=>e.Kind==CombatKind.Damage).Sum(e=>e.Amount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupStatusCannotReenterDiscoveryButLaterRetriesStillWork(bool enumerationFails)
    {
        var enumerations=0;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>
        {
            enumerations++;
            if(enumerationFails)throw new InvalidOperationException("discovery unavailable");
            return Array.Empty<ICaptureDevice>();
        });
        var notifications=0;
        adapter.StatusChanged += _=> {if(notifications++==0)adapter.Start();};
        adapter.Start();
        Assert.Equal(1,enumerations);
        Assert.Equal(1,notifications);
        adapter.Start();
        Assert.Equal(2,enumerations);
        Assert.Equal(2,notifications);
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ShutdownDrainsNativeCallbacksThatPassedTheDisposedCheckBeforeStop()
    {
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var proxy=(StartupDevice)(object)device;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
        var admitted=false;
        var processed=0;
        proxy.OnStop=()=>
        {
            // Simulate a native callback that entered before Dispose marked
            // the adapter disposed, but only now reaches its queue admission.
            try {admitted=Queue(adapter).TryAdd(()=>processed++);}
            catch(InvalidOperationException) { }
        };
        adapter.Start();
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(admitted);
        Assert.Equal(1,processed);
        Assert.True(Queue(adapter).IsAddingCompleted);
    }

    [Fact]
    public async Task NativeStopCanWaitForAnInFlightLifecycleCallbackWithoutDeadlocking()
    {
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var proxy=(StartupDevice)(object)device;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
        var callbackReturned=false;
        var gateHeldDuringStop=false;
        var gate=typeof(LiveCaptureAdapter).GetField("lifecycleGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(adapter)!;
        Task? callback=null;
        proxy.OnStop=()=>
        {
            gateHeldDuringStop=Monitor.IsEntered(gate);
            callback=Task.Run(()=> {adapter.Start();adapter.Dispose();});
            callbackReturned=callback.Wait(TimeSpan.FromSeconds(2));
        };
        adapter.Start();
        adapter.Dispose();
        if(callback!=null)await callback.WaitAsync(TimeSpan.FromSeconds(10));
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(callbackReturned,"Native stop waited on a callback blocked by the lifecycle lock");
        Assert.False(gateHeldDuringStop);
        Assert.Equal(1,proxy.Calls.Count(c=>c=="Close"));
    }

    [Theory]
    [InlineData("add_OnPacketArrival")]
    [InlineData("Open")]
    [InlineData("set_Filter")]
    [InlineData("StartCapture")]
    public async Task FailedStartupCleansDeviceAndStillStartsLaterAdapter(string stage)
    {
        var first=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var later=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var firstProxy=(StartupDevice)(object)first;
        firstProxy.FailOn=stage;
        var laterProxy=(StartupDevice)(object)later;
        var enumerations=0;
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=> {enumerations++;return new[]{first,later};});
        adapter.StatusChanged += _=>throw new Exception("status");
        adapter.Start();
        adapter.Start();
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1,enumerations);
        Assert.Equal(1,firstProxy.Calls.Count(c=>c=="StopCapture"));
        Assert.Equal(1,firstProxy.Calls.Count(c=>c=="Close"));
        Assert.Equal(1,laterProxy.Calls.Count(c=>c=="StartCapture"));
        Assert.Equal(1,laterProxy.Calls.Count(c=>c=="StopCapture"));
        Assert.Equal(1,laterProxy.Calls.Count(c=>c=="Close"));
    }

    [Fact]
    public async Task ConcurrentStartAndDisposeNeverReopenOrLeakDevices()
    {
        for(var i=0;i<100;i++)
        {
            var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
            var proxy=(StartupDevice)(object)device;
            using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
            await Task.WhenAll(Task.Run(adapter.Start),Task.Run(adapter.Dispose),Task.Run(adapter.Start),Task.Run(adapter.Dispose));
            await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            var starts=proxy.Calls.Count(c=>c=="StartCapture");
            Assert.InRange(starts,0,1);
            Assert.Equal(starts,proxy.Calls.Count(c=>c=="Close"));
            adapter.Start();
            Assert.Equal(starts,proxy.Calls.Count(c=>c=="StartCapture"));
        }
    }

    [Fact]
    public async Task DisposeFromStartupFailureStopsFurtherDeviceDiscovery()
    {
        var first=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var later=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        var firstProxy=(StartupDevice)(object)first;
        firstProxy.FailOn="Open";
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{first,later});
        adapter.StatusChanged += message=> {if(message.StartsWith("Adapter unavailable:"))adapter.Dispose();};
        adapter.Start();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("Close",firstProxy.Calls);
        Assert.Empty(((StartupDevice)(object)later).Calls);
    }

    [Fact]
    public async Task DisposeDuringDeviceStartClosesDeviceThatHasNotYetBeenRegistered()
    {
        var device=DispatchProxy.Create<ICaptureDevice,StartupDevice>();
        using var adapter=new LiveCaptureAdapter(new Decoder(),()=>new[]{device});
        var proxy=(StartupDevice)(object)device;
        proxy.OnStart=adapter.Dispose;
        adapter.Start();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("StopCapture",proxy.Calls);
        Assert.Contains("Close",proxy.Calls);
        var devices=(List<ICaptureDevice>)typeof(LiveCaptureAdapter)
            .GetField("devices",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(adapter)!;
        Assert.Empty(devices);
    }

    [Fact]
    public async Task PacketDuplicateAndResetFailuresDoNotStopRecoveryOrLaterPackets()
    {
        using var adapter = new LiveCaptureAdapter(new Decoder());
        var packets = 0;
        var duplicates = 0;
        var resets = 0;
        var hits = 0;
        adapter.PacketCaptured += () => throw new Exception("packet");
        adapter.PacketCaptured += () => packets++;
        adapter.DuplicateSuppressed += () => throw new Exception("duplicate");
        adapter.DuplicateSuppressed += () => duplicates++;
        adapter.ConnectionReset += () => throw new Exception("reset");
        adapter.ConnectionReset += () => resets++;
        adapter.EventReceived += e => { if (e.Kind == CombatKind.Damage) hits++; };
        Queue(adapter).Add(() => Packet(adapter, 1, DateTime.UnixEpoch));
        Queue(adapter).Add(() => Packet(adapter, 1, DateTime.UnixEpoch.AddSeconds(1)));
        Queue(adapter).Add(() => Packet(adapter, 2, DateTime.UnixEpoch.AddSeconds(32)));
        adapter.Dispose();
        await adapter.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(3, packets);
        Assert.Equal(1, duplicates);
        Assert.Equal(1, resets);
        Assert.Equal(4, hits);
    }
}
