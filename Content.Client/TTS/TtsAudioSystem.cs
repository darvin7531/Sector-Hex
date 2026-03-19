using System.IO;
using Content.Shared.TTS;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Client.TTS;

public sealed class TtsAudioSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<TtsAudioEvent>(OnTtsAudio);
    }

    private void OnTtsAudio(TtsAudioEvent ev)
    {
        if (!TryGetEntity(ev.Speaker, out EntityUid? speaker) || speaker == null || TerminatingOrDeleted(speaker.Value))
            return;

        using var stream = new MemoryStream(ev.WavData, writable: false);
        var audioStream = _audioManager.LoadAudioWav(stream, $"tts-{ev.Speaker}-{Guid.NewGuid():N}.wav");
        var audioParams = AudioParams.Default
            .WithVolume(ev.Volume)
            .WithMaxDistance(ev.MaxDistance)
            .WithReferenceDistance(MathF.Min(2f, ev.MaxDistance));

        _audio.PlayEntity(audioStream, speaker.Value, null, audioParams);
    }
}
