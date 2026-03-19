using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.TTS.Components;

namespace Content.Server.Humanoid;

public sealed partial class HumanoidAppearanceSystem
{
    public override void LoadProfile(EntityUid uid, HumanoidCharacterProfile? profile, HumanoidAppearanceComponent? humanoid = null)
    {
        base.LoadProfile(uid, profile, humanoid);

        if (profile == null)
            return;

        var tts = EnsureComp<TtsVoiceComponent>(uid);
        tts.Enabled = true;
        tts.VoiceId = profile.TtsVoiceId;
    }
}
