using System.Windows;
using System.ComponentModel;
using System.Windows.Threading;
using Aion2DPSPro.Capture;
using Aion2DPSPro.Overlay;
using Aion2DPSPro.Protocol;
using Aion2DPSPro.Storage;
using Aion2DPSPro.Updates;

namespace Aion2DPSPro;

public partial class MainWindow : Window
{
    readonly CombatEngine engine=new();
    readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromMilliseconds(250)};
    readonly OverlayWindow overlay=new();
    readonly LiveCaptureAdapter capture;
    readonly object diagnosticsGate=new();
    // Identity is only trusted when the decoder explicitly labels a selfInfo
    // packet. Nearby identityMap records must never promote a player to self.
    long? verifiedLocalActorId;

    readonly Queue<string> inspector=new();
    long packets,decoderDiagnostics,recognized,parsedEvents,duplicates,damageEvents,healEvents,hpEvents,otherEvents,acceptedDamageEvents,snapshotId;
    string activeFlow="Waiting...",captureStatus="Starting capture...",lastDecoder="",lastEvent="",profileId="",validationPath="",historyError="";
    Task saveTail=Task.CompletedTask;
    readonly FightStore store=new();
    private async void CheckForUpdates(object sender, RoutedEventArgs e)
    {
        if(sender is System.Windows.Controls.Button button) button.IsEnabled=false;
        try
        {
            await CheckForUpdatesCoreAsync(showUpToDate:true);
        }
        finally {if(sender is System.Windows.Controls.Button button2)button2.IsEnabled=true;}
    }

    private async Task CheckForUpdatesCoreAsync(bool showUpToDate)
    {
        try
        {
            var update=await GoatMeterUpdater.CheckAsync();
            if(update is null)
            {
                if(showUpToDate)
                    MessageBox.Show(this,"You're running the latest available stable release, or no compatible release was found.","GoatMeter Updates",MessageBoxButton.OK,MessageBoxImage.Information);
                return;
            }
            if(MessageBox.Show(this,$"GoatMeter {update.Version} is available. Download and verify the installer? Your local settings and fight history are stored separately and will be preserved.","GoatMeter Updates",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            var installer=await GoatMeterUpdater.DownloadVerifiedInstallerAsync(update);
            if(MessageBox.Show(this,"Installer downloaded and SHA-256 verified. Close GoatMeter and start the installer now?","GoatMeter Updates",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            GoatMeterUpdater.LaunchInstaller(installer);
            Close();
        }
        catch(Exception ex)
        {
            MessageBox.Show(this,$"Update was not installed: {ex.Message}","GoatMeter Updates",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        overlay.Engine=engine;
        overlay.LocalActorProvider=()=> { lock(diagnosticsGate) return verifiedLocalActorId; };
        // No verified party/force membership API yet: those scopes fail closed.
        overlay.PartyRosterProvider=()=>null;
        overlay.ForceRosterProvider=()=>null;

        overlay.SnapshotProvider=(segment,category)=>engine.Snapshot(segment,category);
        overlay.HistoryProvider=()=>store.List().ToArray();
        overlay.HistoryLoader=path=>store.LoadAsync(path);
        var path=System.IO.Path.Combine(AppContext.BaseDirectory,"Protocol","Profiles","global-current.json");
        ProtocolProfile profile;
        try {profile=ProtocolProfile.Load(path);} catch {profile=ProtocolProfile.SafeGlobalScaffold();}
        var decoder=new CurrentClientDecoder(profile); profileId=decoder.ProfileId;
        var validationDir=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aion2DPSPro","Validation");
        System.IO.Directory.CreateDirectory(validationDir);
        validationPath=System.IO.Path.Combine(validationDir,$"combat-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        int verboseValidationCount=0;
        var validationGate=new object();
        void RecordValidation(string line)
        {
            var tag=line.Split('|').FirstOrDefault(x=>x.StartsWith("tag="))??"tag=other";
            // Discovery packets are sampled; combat, HP, identity and ownership
            // evidence must continue through the entire session and boss kill.
            bool verbose=tag is "tag=identityLifecycle" or "tag=entityBridge" or "tag=targetHpCandidate";
            lock(validationGate)
            {
                if(verbose && verboseValidationCount>=50)return;
                try
                {
                    System.IO.File.AppendAllText(validationPath,line+Environment.NewLine);
                    if(verbose)verboseValidationCount++;
                }
                catch(Exception ex) {lock(diagnosticsGate)captureStatus=$"Validation logging failed: {ex.Message}";}
            }
        }
        decoder.ValidationRecord+=line =>
        {
            // Self-identity validation is emitted only for decoded selfInfo,
            // not ordinary otherInfo or untrusted nearby combat participants.
            if(line.Contains("|tag=selfIdentity|",StringComparison.Ordinal)
                && !line.Contains("|status=no-name",StringComparison.Ordinal))
            {
                var field=line.Split('|').FirstOrDefault(part=>part.StartsWith("entity=",StringComparison.Ordinal));
                if(field!=null && long.TryParse(field.AsSpan("entity=".Length),out var actor) && actor>0)
                    lock(diagnosticsGate) verifiedLocalActorId=actor;
            }
        };
        decoder.ValidationRecord+=RecordValidation;
        decoder.Diagnostic += d => {lock(diagnosticsGate) {decoderDiagnostics++;if(d.Stage=="parse")recognized++;lastDecoder=$"[{d.Stage}] {d.Message} ({d.Bytes} bytes)";}};
        engine.EncounterCompleted += snapshot =>
        {
            // Serialize writes, preserving ordering and reporting failures without stopping capture.
            lock(diagnosticsGate) saveTail=saveTail.ContinueWith(async _=>
            {
                try {await store.SaveAsync(snapshot);} catch(Exception ex) {lock(diagnosticsGate) historyError=$"History save failed: {ex.Message}";}
            }).Unwrap();
        };
        capture=new LiveCaptureAdapter(decoder);
        capture.ValidationRecord+=RecordValidation;
        capture.PacketCaptured += ()=>Interlocked.Increment(ref packets);
        capture.DuplicateSuppressed += ()=>Interlocked.Increment(ref duplicates);
        capture.FlowLocked += s=> {lock(diagnosticsGate)activeFlow=s;};
        capture.ConnectionReset += ()=>
        {
            lock(diagnosticsGate) verifiedLocalActorId=null;
            engine.Apply(new(DateTime.UtcNow,CombatKind.Zone));
        };
        capture.StatusChanged += s=> {lock(diagnosticsGate)captureStatus=s;};
        capture.EventReceived += e=>
        {
            engine.Apply(e);
            lock(diagnosticsGate)
            {
                parsedEvents++;
                lastEvent=$"kind={e.Kind} amount={e.Amount} src={e.SourceId} name={e.Source} tgt={e.TargetId} skill={e.Skill} type={e.DamageType}";
                if(e.Kind==CombatKind.Damage) {damageEvents++;if(e.Amount>0)acceptedDamageEvents++;}
                else if(e.Kind==CombatKind.Heal)healEvents++;
                else if(e.Kind==CombatKind.TargetHp)hpEvents++;
                else otherEvents++;
                inspector.Enqueue($"{e.Utc:HH:mm:ss.fff} {lastEvent}");while(inspector.Count>12)inspector.Dequeue();
            }
        };
        timer.Tick += (_,_)=>Render(); timer.Start(); capture.Start();
        // Startup check is non-blocking. Installation always requires consent.
        Loaded += async (_,_) =>
        {
            await Task.Yield();
            await CheckForUpdatesCoreAsync(showUpToDate:false);
        };
    }
    void OpenOverlay(object sender,RoutedEventArgs e) {if(!overlay.IsVisible)overlay.Show();overlay.Activate();}
    void Reset(object sender,RoutedEventArgs e)=>engine.ResetFight();
    void Render()
    {
        var s=engine.Snapshot();
        snapshotId++; SnapshotState.Text=$"Snapshot #{snapshotId:N0} | FightDamage={s.FightDamage:N0} | FightDPS={s.FightDps:0.##}";
        Dps.Text=$"{s.FightDps:0} DPS"; Damage.Text=$"{s.FightDamage:N0} damage";
        Target.Text=s.Target is null?"No target":$"{s.Target.Name} {s.Target.Percent:0.0}% {s.Target.CurrentHp:N0}/{s.Target.MaxHp:N0}";
        Grid.ItemsSource=s.Players.Select((p,i)=>new {Rank=i+1,p.Name,Dps=p.Dps.ToString("N0"),Damage=p.Damage.ToString("N0"),Share=$"{p.Share:0.0}%"}).ToArray();
        overlay.Render(s);
        lock(diagnosticsGate)
        {
            Status.Text=$"{captureStatus} | {capture.Health}"+(historyError.Length>0?$" | {historyError}":"");
            PacketCount.Text=Interlocked.Read(ref packets).ToString("N0"); DuplicateCount.Text=Interlocked.Read(ref duplicates).ToString("N0");
            DiagnosticCount.Text=decoderDiagnostics.ToString("N0"); RecognizedCount.Text=recognized.ToString("N0"); EventCount.Text=parsedEvents.ToString("N0");
            AcceptedDamageCount.Text=acceptedDamageEvents.ToString("N0"); DamageEventCount.Text=damageEvents.ToString("N0"); HealEventCount.Text=healEvents.ToString("N0");
            HpEventCount.Text=hpEvents.ToString("N0"); OtherEventCount.Text=otherEvents.ToString("N0"); ActiveFlow.Text=activeFlow; Profile.Text=profileId;
            LastDecoder.Text=lastDecoder; LastEvent.Text=LatestEvent.Text=lastEvent; EventInspector.Text=string.Join(Environment.NewLine,inspector.Reverse()); ValidationFile.Text=validationPath;
        }
    }
    bool allowClose,closing;
    protected override async void OnClosing(CancelEventArgs e)
    {
        if(allowClose) {base.OnClosing(e);return;}
        e.Cancel=true;base.OnClosing(e);
        if(closing)return;
        closing=true;timer.Stop();capture.Dispose();
        try
        {
            await capture.Completion;
            engine.ResetFight();
            Task pending;lock(diagnosticsGate)pending=saveTail;
            await pending;
            if(historyError.Length>0)MessageBox.Show(this,historyError,"History was not saved");
        }
        catch(Exception ex) {MessageBox.Show(this,ex.Message,"Unable to finish saving history");}
        overlay.Close();allowClose=true;Close();
    }
}
