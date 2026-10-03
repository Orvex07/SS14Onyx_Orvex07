// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Eui;
using Content.Shared._Onyx.ReadyManifest;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Onyx.ReadyManifest;

[UsedImplicitly]
public sealed class ReadyManifestEui : BaseEui
{
    private readonly ReadyManifestWindow _window = new();

    public ReadyManifestEui()
    {
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is ReadyManifestEuiState manifestState)
            _window.Populate(manifestState.Entries);
    }
}
