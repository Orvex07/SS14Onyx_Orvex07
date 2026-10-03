// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared._Onyx.AlternativeJobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.ReadyManifest;

[Serializable, NetSerializable]
public sealed class ReadyManifestRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class ReadyManifestEuiState(ReadyManifestEntry[] entries) : EuiStateBase
{
    public ReadyManifestEntry[] Entries { get; } = entries;
}

[Serializable, NetSerializable]
public sealed class ReadyManifestEntry(
    string characterName,
    ProtoId<JobPrototype> job,
    ProtoId<AlternativeJobPrototype>? alternativeJob,
    JobPriority priority)
{
    public string CharacterName { get; } = characterName;
    public ProtoId<JobPrototype> Job { get; } = job;
    public ProtoId<AlternativeJobPrototype>? AlternativeJob { get; } = alternativeJob;
    public JobPriority Priority { get; } = priority;
}
