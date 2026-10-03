// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Onyx.ReadyManifest;
using Robust.Client.UserInterface.Controls;

#pragma warning disable IDE0130
namespace Content.Client.Lobby;

public sealed partial class LobbyState
{
    private ReadyManifestSystem _readyManifest = default!;

    private void InitializeReadyManifest()
    {
        _readyManifest = _entityManager.System<ReadyManifestSystem>();
        Lobby!.ReadyManifestButton.OnPressed += OnReadyManifestPressed;
    }

    private void ShutdownReadyManifest()
    {
        Lobby!.ReadyManifestButton.OnPressed -= OnReadyManifestPressed;
    }

    private void UpdateReadyManifest()
    {
        Lobby!.ReadyManifestButton.Visible = !_gameTicker.IsGameStarted;
    }

    private void OnReadyManifestPressed(BaseButton.ButtonEventArgs args)
    {
        if (!_gameTicker.IsGameStarted)
            _readyManifest.RequestManifest();
    }
}
