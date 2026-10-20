namespace Aion2DPSPro.Protocol;

public sealed class CurrentClientDecoder : IAion2Decoder
{
    private readonly ProtocolProfile profile;
    private readonly Dictionary<string, StreamFramer> framers = new();
    private PacketDispatcher dispatcher;
    public string ProfileId => profile.Id;
    public event Action<DecoderDiagnostic>? Diagnostic;
    public event Action<string>? ValidationRecord;

    public CurrentClientDecoder(ProtocolProfile? profile = null)
    {
        this.profile = profile ?? ProtocolProfile.SafeGlobalScaffold();
        dispatcher = new PacketDispatcher(this.profile);

        WireDispatcher();
    }

    public CurrentClientDecoder CreateSibling()
    {
        var child=new CurrentClientDecoder(profile);
        child.Diagnostic += ReportDiagnosticSafely;
        child.ValidationRecord += ReportValidationSafely;
        return child;
    }
    public void ResetStream(string stream) => framers.Remove(stream);
    public void ResetConnection() { framers.Clear(); dispatcher = new PacketDispatcher(profile); WireDispatcher(); }
    private void WireDispatcher()
    {
        dispatcher.Diagnostic += ReportDiagnosticSafely;
        dispatcher.ValidationRecord += ReportValidationSafely;
    }
    private void ReportDiagnosticSafely(DecoderDiagnostic diagnostic)
    {
        var handlers=Diagnostic;
        if(handlers==null)return;
        foreach(Action<DecoderDiagnostic> handler in handlers.GetInvocationList())
        {
            try {handler(diagnostic);}
            catch { /* A diagnostic failure must not interrupt decoding or recurse. */ }
        }
    }
    private void ReportValidationSafely(string record)
    {
        var handlers=ValidationRecord;
        if(handlers==null)return;
        foreach(Action<string> handler in handlers.GetInvocationList())
        {
            try {handler(record);}
            catch(Exception ex)
            {
                ReportDiagnosticSafely(new(DateTime.UtcNow,"subscriber",$"Validation notification failed: {ex.Message}",0));
            }
        }
    }
    public IEnumerable<Aion2Decoded> Decode(byte[] payload, DateTime utc) => DecodeStream("default",payload,utc);
    public IEnumerable<Aion2Decoded> DecodeStream(string stream, byte[] payload, DateTime utc)
    {
        if(!framers.TryGetValue(stream,out var framer))
        {
            framers[stream]=framer=new StreamFramer();
            framer.Diagnostic += ReportDiagnosticSafely;
        }
        foreach(var frame in framer.Push(payload,utc))
            foreach(var evt in dispatcher.Dispatch(frame,utc)) yield return dispatcher.AttachRosterScores(evt,utc);
    }
}
