// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.ReadyManifest;

namespace Content.Client._Onyx.ReadyManifest;

public sealed class ReadyManifestSystem : EntitySystem
{
    public void RequestManifest()
    {
        RaiseNetworkEvent(new ReadyManifestRequestEvent());
    }
}
