using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Shared.Player;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        public const float PresetFailedCooldownIncrease = 30f;

        private Task<int>? _pendingRoundIdTask;

        /// <summary>
        /// The selected preset that will be used at the start of the next round.
        /// </summary>
        public GamePresetPrototype? Preset { get; private set; }

        /// <summary>
        /// The preset that's currently active.
        /// </summary>
        public GamePresetPrototype? CurrentPreset { get; private set; }

        private bool StartPreset(ICommonSession[] origReadyPlayers, bool force)
        {
            var startAttempt = new RoundStartAttemptEvent(origReadyPlayers, force);
            RaiseLocalEvent(startAttempt);

            if (!startAttempt.Cancelled)
                return true;

            var presetTitle = CurrentPreset != null ? Loc.GetString(CurrentPreset.ModeTitle) : string.Empty;

            void FailedPresetRestart()
            {
                SendServerMessage(Loc.GetString("game-ticker-start-round-cannot-start-game-mode-restart",
                    ("failedGameMode", presetTitle)));
                RestartRound();
                DelayStart(TimeSpan.FromSeconds(PresetFailedCooldownIncrease));
            }

            if (_cfg.GetCVar(CCVars.GameLobbyFallbackEnabled))
            {
                var fallbackPresets = _cfg.GetCVar(CCVars.GameLobbyFallbackPreset).Split(",");
                var startFailed = true;

                foreach (var preset in fallbackPresets)
                {
                    ClearGameRules();
                    SetGamePreset(preset);
                    AddGamePresetRules();
                    StartGamePresetRules();

                    startAttempt.Uncancel();
                    RaiseLocalEvent(startAttempt);

                    if (!startAttempt.Cancelled)
                    {
                        _chatManager.SendAdminAnnouncement(
                            Loc.GetString("game-ticker-start-round-cannot-start-game-mode-fallback",
                                ("failedGameMode", presetTitle),
                                ("fallbackMode", Loc.GetString(preset))));
                        RefreshLateJoinAllowed();
                        startFailed = false;
                        break;
                    }
                }

                if (startFailed)
                {
                    FailedPresetRestart();
                    return false;
                }
            }

            else
            {
                FailedPresetRestart();
                return false;
            }

            return true;
        }

        private void InitializeGamePreset()
        {
            SetGamePreset(LobbyEnabled ? _cfg.GetCVar(CCVars.GameLobbyDefaultPreset) : "sandbox");
        }

        public void SetGamePreset(GamePresetPrototype? preset, bool force = false)
        {
            // Do nothing if this game ticker is a dummy!
            if (DummyTicker)
                return;

            Preset = preset;
            ValidateMap();
            UpdateInfoText();

            if (force)
            {
                StartRound(true);
            }
        }

        public void SetGamePreset(string preset, bool force = false)
        {
            var proto = FindGamePreset(preset);
            if(proto != null)
                SetGamePreset(proto, force);
        }

        public GamePresetPrototype? FindGamePreset(string preset)
        {
            if (_prototypeManager.TryIndex(preset, out GamePresetPrototype? presetProto))
                return presetProto;

            foreach (var proto in _prototypeManager.EnumeratePrototypes<GamePresetPrototype>())
            {
                foreach (var alias in proto.Alias)
                {
                    if (preset.Equals(alias, StringComparison.InvariantCultureIgnoreCase))
                        return proto;
                }
            }

            return null;
        }

        public bool TryFindGamePreset(string preset, [NotNullWhen(true)] out GamePresetPrototype? prototype)
        {
            prototype = FindGamePreset(preset);

            return prototype != null;
        }

        public bool IsMapEligible(GameMapPrototype map)
        {
            if (Preset == null)
                return true;

            if (Preset.MapPool == null || !_prototypeManager.TryIndex<GameMapPoolPrototype>(Preset.MapPool, out var pool))
                return true;

            return pool.Maps.Contains(map.ID);
        }

        private void ValidateMap()
        {
            if (Preset == null || _gameMapManager.GetSelectedMap() is not { } map)
                return;

            if (Preset.MapPool == null ||
                !_prototypeManager.TryIndex<GameMapPoolPrototype>(Preset.MapPool, out var pool))
                return;

            if (pool.Maps.Contains(map.ID))
                return;

            _gameMapManager.SelectMapRandom();
        }

        [PublicAPI]
        private bool AddGamePresetRules()
        {
            if (DummyTicker || Preset == null)
                return false;

            CurrentPreset = Preset;
            foreach (var rule in Preset.Rules)
            {
                TryAddGameRule(rule); // Exodus
            }

            return true;
        }

        public void StartGamePresetRules()
        {
            // May be touched by the preset during init.
            var rules = new List<EntityUid>(GetAddedGameRules());
            foreach (var rule in rules)
            {
                StartGameRule(rule);
            }
        }

        private void BeginIncrementRoundNumber()
        {
            if (_pendingRoundIdTask is { IsCompleted: false })
                return;

            var playerIds = _playerGameStatuses.Keys.Select(player => player.UserId).ToArray();
            _pendingRoundIdTask = Task.Run(async () =>
            {
                var server = await _dbEntryManager.ServerEntity;
                return await _db.AddNewRound(server, playerIds);
            });
        }

        private bool TryAssignRoundNumber()
        {
            if (_pendingRoundIdTask == null)
                BeginIncrementRoundNumber();

            if (_pendingRoundIdTask == null || !_pendingRoundIdTask.IsCompleted)
                return false;

            if (_pendingRoundIdTask.IsFaulted)
            {
                Log.Error($"Failed to allocate next round id: {_pendingRoundIdTask.Exception}");
                _pendingRoundIdTask = null;
                return false;
            }

            if (_pendingRoundIdTask.IsCanceled)
            {
                Log.Warning("Next round id allocation was canceled. Retrying.");
                _pendingRoundIdTask = null;
                return false;
            }

            RoundId = _pendingRoundIdTask.GetAwaiter().GetResult();
            _pendingRoundIdTask = null;
            return true;
        }
    }
}
