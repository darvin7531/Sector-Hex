using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Shared.CCVar;
using Content.Shared.TTS;
using Content.Shared.TTS.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.TTS.Systems;

public sealed class TtsSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly HttpClient _http = new();
    private readonly Dictionary<EntityUid, TimeSpan> _lastRequest = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
    }

    public override void Shutdown()
    {
        _http.Dispose();
        base.Shutdown();
    }

    private void OnEntitySpoke(EntitySpokeEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.TtsEnabled))
            return;

        if (string.IsNullOrWhiteSpace(ev.Message))
            return;

        if (!_cfg.GetCVar(CCVars.TtsIncludeRadio) && ev.Channel != null)
            return;

        if (!_cfg.GetCVar(CCVars.TtsIncludeWhispers) && ev.IsWhisper)
            return;

        var message = ev.Message.Trim();
        if (message.Length > _cfg.GetCVar(CCVars.TtsMaxTextLength))
            return;

        if (!TryResolveVoice(ev.Source, ev, out var voiceId, out var mode, out var speed, out var volume, out var maxDistance))
            return;

        var cooldown = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.TtsSpeakerCooldownSeconds));
        if (cooldown > TimeSpan.Zero &&
            _lastRequest.TryGetValue(ev.Source, out var lastRequest) &&
            _timing.CurTime - lastRequest < cooldown)
        {
            return;
        }

        _lastRequest[ev.Source] = _timing.CurTime;
        _ = HandleSpeechAsync(ev.Source, message, voiceId, mode, speed, volume, maxDistance);
    }

    private bool TryResolveVoice(
        EntityUid speaker,
        EntitySpokeEvent ev,
        out string voiceId,
        out string mode,
        out float speed,
        out float volume,
        out float maxDistance)
    {
        voiceId = _cfg.GetCVar(CCVars.TtsDefaultVoice).Trim();
        mode = "cross_lingual";
        speed = 1f;
        volume = _cfg.GetCVar(CCVars.TtsDefaultVolume);
        maxDistance = _cfg.GetCVar(CCVars.TtsDefaultMaxDistance);

        if (TryComp<TtsVoiceComponent>(speaker, out var comp))
        {
            if (!comp.Enabled)
                return false;

            if (ev.IsWhisper && !comp.IncludeWhispers && !_cfg.GetCVar(CCVars.TtsIncludeWhispers))
                return false;

            if (ev.Channel != null && !comp.IncludeRadio && !_cfg.GetCVar(CCVars.TtsIncludeRadio))
                return false;

            if (!string.IsNullOrWhiteSpace(comp.VoiceId))
                voiceId = comp.VoiceId.Trim();

            if (!string.IsNullOrWhiteSpace(comp.Mode))
                mode = comp.Mode.Trim();

            speed = comp.Speed;
            volume = comp.Volume;
            maxDistance = comp.MaxDistance;
        }

        if (string.IsNullOrWhiteSpace(voiceId))
            return false;

        mode = mode is "cross_lingual" or "zero_shot" ? mode : "cross_lingual";
        speed = Math.Clamp(speed, 0.5f, 1.5f);
        maxDistance = Math.Clamp(maxDistance, 1f, 30f);
        return true;
    }

    private async Task HandleSpeechAsync(
        EntityUid speaker,
        string message,
        string voiceId,
        string mode,
        float speed,
        float volume,
        float maxDistance)
    {
        try
        {
            var apiUrl = _cfg.GetCVar(CCVars.TtsApiUrl).Trim();
            if (string.IsNullOrWhiteSpace(apiUrl))
                return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.TtsRequestTimeoutSeconds)));
            using var response = await _http.PostAsJsonAsync(apiUrl, new
            {
                voice_id = voiceId,
                text = message,
                mode,
                speed,
            }, cts.Token);

            if (!response.IsSuccessStatusCode)
                return;

            var wavData = await response.Content.ReadAsByteArrayAsync(cts.Token);
            if (wavData.Length == 0)
                return;

            if (TerminatingOrDeleted(speaker))
                return;

            var filter = Filter.Pvs(speaker, entityManager: EntityManager);
            if (filter.Count == 0)
                return;

            RaiseNetworkEvent(
                new TtsAudioEvent(GetNetEntity(speaker), wavData, volume, maxDistance),
                filter);
        }
        catch
        {
            // Keep chat responsive even when TTS backend is unavailable.
        }
    }
}
