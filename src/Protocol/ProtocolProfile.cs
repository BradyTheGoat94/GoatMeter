using System.IO;
using System.Text.Json;
namespace Aion2DPSPro.Protocol;

public sealed record PacketTag(byte A, byte B);
public sealed record ProtocolProfile(
    string Id,
    string Region,
    string ClientBuild,
    int GamePort,
    IReadOnlyDictionary<string, PacketTag> Tags,
    bool Verified = false,
    bool ExperimentalRosterScores = false)
{
    public static ProtocolProfile SafeGlobalScaffold() => new(
        "GLOBAL-CURRENT-NEEDS-CAPTURE-VALIDATION", "Global", "current", 13328,
        new Dictionary<string,PacketTag>(), false);

    public static ProtocolProfile Load(string path)
    {
        var profile=JsonSerializer.Deserialize<ProtocolProfile>(File.ReadAllText(path), new JsonSerializerOptions{PropertyNameCaseInsensitive=true})
            ?? throw new InvalidDataException("Invalid protocol profile");
        // Syntactically valid JSON can still omit a required map or contain
        // null entries. Let startup use its existing fallback rather than
        // accepting a profile that throws on every captured packet.
        if(profile.Tags==null || profile.Tags.Values.Any(tag=>tag==null))
            throw new InvalidDataException("Protocol profile needs a tag map with non-null packet tags.");
        return profile;
    }
}
public sealed record DecoderDiagnostic(DateTime Utc, string Stage, string Message, int Bytes = 0);

