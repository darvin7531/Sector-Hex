using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.TTS;

[Serializable, NetSerializable]
public sealed class TtsAudioEvent : EntityEventArgs
{
    public NetEntity Speaker;
    public byte[] WavData;
    public float Volume;
    public float MaxDistance;

    public TtsAudioEvent(NetEntity speaker, byte[] wavData, float volume, float maxDistance)
    {
        Speaker = speaker;
        WavData = wavData;
        Volume = volume;
        MaxDistance = maxDistance;
    }
}
