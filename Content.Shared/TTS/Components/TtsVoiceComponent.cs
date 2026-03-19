using Robust.Shared.GameObjects;

namespace Content.Shared.TTS.Components;

[RegisterComponent]
public sealed partial class TtsVoiceComponent : Component
{
    [DataField]
    public bool Enabled = true;

    [DataField]
    public string VoiceId = string.Empty;

    [DataField]
    public string Mode = "cross_lingual";

    [DataField]
    public float Speed = 1f;

    [DataField]
    public float Volume = 0f;

    [DataField]
    public float MaxDistance = 10f;

    [DataField]
    public bool IncludeWhispers;

    [DataField]
    public bool IncludeRadio;
}
