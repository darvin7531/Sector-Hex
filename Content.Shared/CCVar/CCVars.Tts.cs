using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> TtsEnabled =
        CVarDef.Create("tts.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> TtsApiUrl =
        CVarDef.Create("tts.api_url", "http://127.0.0.1:8030/synthesize", CVar.SERVERONLY);

    public static readonly CVarDef<string> TtsDefaultVoice =
        CVarDef.Create("tts.default_voice", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> TtsAvailableVoices =
        CVarDef.Create("tts.available_voices", string.Empty, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> TtsMaxTextLength =
        CVarDef.Create("tts.max_text_length", 220, CVar.SERVERONLY);

    public static readonly CVarDef<float> TtsRequestTimeoutSeconds =
        CVarDef.Create("tts.request_timeout", 15f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TtsSpeakerCooldownSeconds =
        CVarDef.Create("tts.speaker_cooldown", 0.35f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> TtsIncludeWhispers =
        CVarDef.Create("tts.include_whispers", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> TtsIncludeRadio =
        CVarDef.Create("tts.include_radio", false, CVar.SERVERONLY);

    public static readonly CVarDef<float> TtsDefaultVolume =
        CVarDef.Create("tts.default_volume", 0f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TtsDefaultMaxDistance =
        CVarDef.Create("tts.default_max_distance", 10f, CVar.SERVERONLY);
}
