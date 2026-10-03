// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared._Onyx.AlternativeJobs;
using Content.Shared._Onyx.ReadyManifest;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.ReadyManifest;

public sealed partial class ReadyManifestSystem : EntitySystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private ServerGameTicker _gameTicker = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly Dictionary<ICommonSession, ReadyManifestEui> _openEuis = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ReadyManifestRequestEvent>(OnRequest);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<ReadyManifestChangedEvent>(OnManifestChanged);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnRequest(ReadyManifestRequestEvent message, EntitySessionEventArgs args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        if (_openEuis.TryGetValue(args.SenderSession, out var openEui))
        {
            openEui.StateDirty();
            return;
        }

        var eui = new ReadyManifestEui(this);
        _openEuis.Add(args.SenderSession, eui);
        _eui.OpenEui(eui, args.SenderSession);
        eui.StateDirty();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New == GameRunLevel.PreRoundLobby)
            return;

        foreach (var eui in _openEuis.Values.ToArray())
        {
            eui.Close();
        }

        _openEuis.Clear();
    }

    private void OnManifestChanged(ReadyManifestChangedEvent args)
    {
        foreach (var eui in _openEuis.Values)
        {
            eui.StateDirty();
        }
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent args)
    {
        OnManifestChanged(new ReadyManifestChangedEvent());
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            OnManifestChanged(new ReadyManifestChangedEvent());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
        _openEuis.Clear();
    }

    public ReadyManifestEntry[] BuildEntries()
    {
        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return [];

        var entries = new List<ReadyManifestEntry>();
        foreach (var session in _player.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected
                || !_gameTicker.PlayerGameStatuses.TryGetValue(session.UserId, out var status)
                || status != PlayerGameStatus.ReadyToPlay
                || !_preferences.TryGetCachedPreferences(session.UserId, out var preferences)
                || preferences.SelectedCharacter is not HumanoidCharacterProfile profile)
                continue;

            foreach (var (job, priority) in profile.JobPriorities)
            {
                if (priority == JobPriority.Never)
                    continue;

                ProtoId<AlternativeJobPrototype>? alternativeJob = null;
                if (profile.JobAlternatives.TryGetValue(job, out var alternative))
                    alternativeJob = alternative;
                entries.Add(new ReadyManifestEntry(profile.Name, job, alternativeJob, priority));
            }
        }

        return entries.ToArray();
    }

    public void RemoveEui(ICommonSession player, ReadyManifestEui eui)
    {
        if (_openEuis.GetValueOrDefault(player) == eui)
            _openEuis.Remove(player);
    }
}
