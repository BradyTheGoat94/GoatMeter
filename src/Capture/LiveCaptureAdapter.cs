using SharpPcap;
using PacketDotNet;
using Aion2DPSPro.Protocol;
using System.Collections.Concurrent;

namespace Aion2DPSPro.Capture;

/// <summary>Capture callbacks only queue bytes. A single worker owns decoding and flow state.</summary>
public sealed class LiveCaptureAdapter : IDisposable
{
    private readonly IAion2Decoder decoder;
    private readonly List<ICaptureDevice> devices=new();
    private readonly CaptureIdentityBridge identityBridge=new();
    private string? lockedScope;
    private readonly TcpStreamReassembler reassembler=new();
    private readonly BlockingCollection<Action> queue=new(4096);
    private readonly Task worker;
    private readonly Dictionary<string,(CurrentClientDecoder Decoder, DateTime Seen)> candidates=new();
    private string? lockedDevice,lockedConversation;
    private DateTime lastPayload,lastEvent;
    private long dropped;
    private readonly object lifecycleGate=new();
    private volatile bool disposed;
    public Task Completion => worker;
    public event Action<string>? ValidationRecord;
    public event Action<CombatEvent>? EventReceived;
    public event Action<string>? StatusChanged;
    public event Action? PacketCaptured;
    public event Action? DuplicateSuppressed;
    public event Action<string>? FlowLocked;
    public event Action? ConnectionReset;
    public string Health => Interlocked.Read(ref dropped)>0 ? $"Capture overloaded: {Interlocked.Read(ref dropped)} packets dropped; accuracy incomplete" :
        lockedConversation==null ? "Waiting for a validated game conversation" : DateTime.UtcNow-lastPayload>TimeSpan.FromSeconds(15) ? "Capture idle / disconnected" :
        DateTime.UtcNow-lastEvent>TimeSpan.FromSeconds(30) ? "Packets arriving; no decoded events (idle game or decoder mismatch)" : "Capture and decoding active; verified for tested gameplay";
    public LiveCaptureAdapter(IAion2Decoder decoder)
    {
        this.decoder=decoder;
        // Diagnostic subscribers must not interrupt the packet-processing worker.
        reassembler.DuplicateDiscarded += ()=>
        {
            try { DuplicateSuppressed?.Invoke(); }
            catch(Exception ex) { ReportStatusSafely($"Duplicate notification failed: {ex.Message}"); }
        };
        reassembler.StreamReset += key =>
        {
            if(selectedDecoder is CurrentClientDecoder selected) selected.ResetStream(key);
            if(decoder is CurrentClientDecoder c) c.ResetStream(key);
            foreach(var candidate in candidates.Values) candidate.Decoder.ResetStream(key);
            ReportStatusSafely("TCP gap/flow reset; incomplete data discarded.");
        };
        worker=Task.Run(()=>
        {
            foreach(var action in queue.GetConsumingEnumerable())
            {
                try { action(); }
                catch(Exception ex)
                {
                    // UI/diagnostic subscribers can throw too. A failed status
                    // callback must not terminate the only packet-processing worker.
                    ReportStatusSafely($"Capture/decoder error: {ex.Message}");
                }
            }
        });
    }
    private void ReportStatusSafely(string message)
    {
        try { StatusChanged?.Invoke(message); }
        catch { /* Reporting failures must not terminate capture. */ }
    }
    public void Start()
    {
        lock(lifecycleGate)
        {
        if(disposed || devices.Count>0)return;
        try
        {
            foreach(var d in CaptureDeviceList.Instance)
            {
                try { d.OnPacketArrival+=OnPacket; d.Open(DeviceModes.Promiscuous,1000); d.Filter="tcp port 13328"; d.StartCapture(); devices.Add(d); }
                catch(Exception ex)
                {
                    // A failed subscription removal must not abort discovery of
                    // the remaining capture devices.
                    try { d.OnPacketArrival-=OnPacket; } catch { }
                    try { d.StopCapture(); } catch { }
                    try { d.Close(); } catch { }
                    ReportStatusSafely($"Adapter unavailable: {ex.Message}");
                }
            }
            ReportStatusSafely(devices.Count==0?"No capture adapter available. Install Npcap.":$"Waiting for validated AION traffic ({devices.Count} adapters).");
        }
        catch(Exception ex) { ReportStatusSafely($"Capture startup failed: {ex.Message}"); }
        }
    }
    private void OnPacket(object sender,PacketCapture capture)
    {
        if(disposed) return;
        try
        {
            var raw=capture.GetPacket();
            var bytes=raw.Data.ToArray(); var link=raw.LinkLayerType;
            var device=(sender as ICaptureDevice)?.Name??"unknown";
            var utc=DateTime.UtcNow;
            if(!queue.TryAdd(()=>Process(device,Packet.ParsePacket(link,bytes),utc)))
                Interlocked.Increment(ref dropped);
        }
        catch(InvalidOperationException)
        {
            // Queue shutdown is expected only during disposal. Otherwise this
            // indicates capture loss and must be surfaced to the user.
            if(!disposed) Interlocked.Increment(ref dropped);
        }
        catch(Exception ex)
        {
            Interlocked.Increment(ref dropped);
            // Never let a malformed capture escape the native packet callback.
            ReportStatusSafely($"Capture callback error; data may be incomplete: {ex.Message}");
        }
    }
    private void ResetConnection()
    {
        lockedDevice=null; lockedConversation=null; lockedScope=null; selectedDecoder=null; reassembler.Reset(); candidates.Clear(); identityBridge.Clear();
        if(decoder is CurrentClientDecoder c) c.ResetConnection();
        // A UI subscriber failure must not abort connection recovery.
        try { ConnectionReset?.Invoke(); }
        catch(Exception ex) { ReportStatusSafely($"Connection reset notification failed: {ex.Message}"); }
        ReportStatusSafely("Connection reset; searching for validated game traffic.");
    }
    private void Process(string device,Packet packet,DateTime utc)
    {
        var tcp=packet.Extract<TcpPacket>(); var ip=packet.Extract<IPPacket>();
        if(tcp==null || ip==null) return;
        string source=$"{ip.SourceAddress}:{tcp.SourcePort}",destination=$"{ip.DestinationAddress}:{tcp.DestinationPort}";
        string conversation=string.CompareOrdinal(source,destination)<=0?$"{source}<>{destination}":$"{destination}<>{source}";
        string direction=$"{device}|{source}>{destination}";
        var server=tcp.SourcePort==13328?ip.SourceAddress:ip.DestinationAddress;
        var local=tcp.SourcePort==13328?ip.DestinationAddress:ip.SourceAddress;
        string scope=$"{device}|{local}|{server}";
        if(lockedConversation!=null && utc-lastPayload>TimeSpan.FromSeconds(30)) ResetConnection();
        if(lockedScope!=null && scope!=lockedScope) return;
        bool primary=lockedConversation==conversation && lockedDevice==device;
        string candidateKey=device+conversation;
        if(tcp.Synchronize || tcp.Finished || tcp.Reset)
        {
            if(primary) {ResetConnection();primary=false;}
            else {candidates.Remove(candidateKey);reassembler.Remove(direction);}
            if(tcp.Finished || tcp.Reset)return;
        }
        if(tcp.PayloadData is not {Length:>0})return;
        // Packet-count UI listeners are diagnostics, not part of decoding.
        try { PacketCaptured?.Invoke(); }
        catch(Exception ex) { ReportStatusSafely($"Packet notification failed: {ex.Message}"); }
        IAion2Decoder active=primary?selectedDecoder??decoder:decoder;
        if(!primary && decoder is CurrentClientDecoder baseDecoder)
        {
            if(!candidates.TryGetValue(candidateKey,out var candidate))
            {
                if(candidates.Count>=8)candidates.Remove(candidates.MinBy(x=>x.Value.Seen).Key);
                candidate=(baseDecoder.CreateSibling(),utc);
            }
            candidates[candidateKey]=(candidate.Decoder,utc);active=candidate.Decoder;
        }
        if(primary)lastPayload=utc;
        var chunks=reassembler.Push(direction,unchecked(tcp.SequenceNumber+(tcp.Synchronize?1u:0u)),tcp.PayloadData,utc);
        if(chunks.Count==0)return;
        var decoded=new List<CombatEvent>();
        foreach(var chunk in chunks)
        foreach(var d in active is CurrentClientDecoder c?c.DecodeStream(direction,chunk,utc):active.Decode(chunk,utc))
            decoded.Add(new(utc,d.Kind,d.SourceId,d.Source,d.TargetId,d.Target,d.Skill,d.Amount,d.DamageType,d.CurrentHp,d.MaxHp,d.Effect,d.Stacks,d.SourceClass,d.DamageFlags,SourceIdentityConfirmed:d.SourceIdentityConfirmed,SourceServerId:d.SourceServerId,SourceCharacterId:d.SourceCharacterId,SourceGearScore:d.SourceGearScore,SourceCombatPower:d.SourceCombatPower,StatsObservedUtc:d.StatsObservedUtc));
        foreach(var e in decoded.Where(x=>x.Kind is CombatKind.PlayerName or CombatKind.TargetHp or CombatKind.CharacterStats || primary&&x.Kind==CombatKind.Despawn))identityBridge.Observe(scope,e);
        foreach(var e in decoded.Where(x=>x.Kind==CombatKind.PlayerName))ValidationRecord?.Invoke($"{utc:O}|tag=captureIdentity|entity={e.SourceId}|name={e.Source}|scope={scope}|conversation={conversation}|primary={primary}");
        if(lockedConversation!=null&&decoded.Any(x=>x.Kind==CombatKind.CharacterStats))
            foreach(var identity in identityBridge.Identities(scope,utc))EventReceived?.Invoke(identity);
        if(lockedConversation==null)
        {
            if(!decoded.Any(x=>(x.Kind is CombatKind.Damage or CombatKind.Heal)&&x.Amount>0))return;
            lockedDevice=device;lockedConversation=conversation;lockedScope=scope;selectedDecoder=active;primary=true;
            // Publish identities captured before combat lock, including identity-only sibling connections.
            foreach(var identity in identityBridge.Identities(scope,utc))EventReceived?.Invoke(identity);
            FlowLocked?.Invoke($"{device} | {conversation}");
        }
        if(!primary)
        {
            foreach(var identity in decoded.Where(x=>x.Kind==CombatKind.PlayerName))EventReceived?.Invoke(identityBridge.Resolve(scope,identity));
            return;
        }
        lastPayload=utc;if(decoded.Count>0)lastEvent=utc;
        foreach(var e in decoded.Where(x=>x.Kind!=CombatKind.CharacterStats))
        {
            var resolved=identityBridge.Resolve(scope,e);
            if(e.Kind is CombatKind.Damage or CombatKind.Heal)
                ValidationRecord?.Invoke($"{utc:O}|tag=resolvedCombat|entity={resolved.SourceId}|name={resolved.Source}|class={resolved.SourceClass}|confirmed={resolved.SourceIdentityConfirmed}|target={resolved.TargetId}|scope={scope}|conversation={conversation}");
            EventReceived?.Invoke(resolved);
        }
    }
    private IAion2Decoder? selectedDecoder;
    public void Dispose()
    {
        lock(lifecycleGate)
        {
        if(disposed)return;
        disposed=true;
        foreach(var d in devices)
        {
            // Native capture teardown can fail on an already-disconnected adapter.
            // Still attempt to stop and close every remaining adapter.
            try { d.OnPacketArrival-=OnPacket; }
            catch(Exception ex) { ReportStatusSafely($"Capture unsubscribe failed: {ex.Message}"); }
            try { d.StopCapture(); }
            catch(Exception ex) { ReportStatusSafely($"Capture stop failed: {ex.Message}"); }
            try { d.Close(); }
            catch(Exception ex) { ReportStatusSafely($"Capture close failed: {ex.Message}"); }
        }
        devices.Clear(); queue.CompleteAdding();
        // Do not dispose the queue while a late native callback may still be
        // returning from TryAdd. Completion lets the worker drain safely;
        // the queue is released with the adapter after callbacks have stopped.
        // UI never blocks waiting for work that may post UI updates.
        }
    }
}
