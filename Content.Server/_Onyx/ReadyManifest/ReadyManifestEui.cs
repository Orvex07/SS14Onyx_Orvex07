// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.EUI;
using Content.Shared._Onyx.ReadyManifest;

namespace Content.Server._Onyx.ReadyManifest;

public sealed class ReadyManifestEui(ReadyManifestSystem system) : BaseEui
{
    public override ReadyManifestEuiState GetNewState()
    {
        return new ReadyManifestEuiState(system.BuildEntries());
    }

    public override void Closed()
    {
        base.Closed();
        system.RemoveEui(Player, this);
    }
}
