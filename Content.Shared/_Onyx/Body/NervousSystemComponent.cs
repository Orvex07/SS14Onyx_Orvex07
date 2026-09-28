// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Onyx.Wounds;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Body;

/// <summary>
/// Body-level nervous system hub (lives on the brain organ).
/// Aggregates pain from per-nerve modifiers, mirroring the Goob-Station nerve model:
/// parts carry <see cref="Wounds.NerveComponent"/> anchors, all felt pain is summed here.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NervousSystemComponent : Component
{
    /// <summary>
    /// Total felt pain aggregated from <see cref="Modifiers"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Pain;

    /// <summary>
    /// Last pain value perceived by the body after numbness.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 LastFeltPain;

    /// <summary>
    /// How much wound pain this hub can hold; the wound part is clamped to it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 SoftPainCap = 120;

    /// <summary>
    /// Hard cap for total pain.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 PainCap = 200;

    /// <summary>
    /// Nerves (body parts) parented to this hub. Filled up upon initialization.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> Nerves = new();

    /// <summary>
    /// Pain contributions keyed by (nerve, identifier). Do not add manually, use PainSystem.
    /// </summary>
    [ViewVariables]
    public Dictionary<(EntityUid, string), PainModifier> Modifiers = new();

    /// <summary>
    /// Pain multipliers keyed by identifier. Do not add manually, use PainSystem.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, PainMultiplier> Multipliers = new();

    [DataField]
    public FixedPoint2 LastPainThreshold;

    [ViewVariables]
    public PainThresholdTypes LastThresholdType = PainThresholdTypes.None;

    [DataField]
    public TimeSpan ThresholdUpdateTime = TimeSpan.FromSeconds(1.6f);

    [DataField]
    public TimeSpan PainReactionTime = TimeSpan.FromSeconds(0.07f);

    [DataField]
    public TimeSpan PainShockAdrenalineTime = TimeSpan.FromSeconds(30f);

    [DataField]
    public TimeSpan PainShockStunTime = TimeSpan.FromSeconds(2f);

    [ViewVariables]
    public TimeSpan UpdateTime;

    [ViewVariables]
    public TimeSpan ReactionUpdateTime;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan NextCritScream;

    [ViewVariables]
    public MobState LastMobState = MobState.Alive;

    [DataField]
    public Dictionary<PainThresholdTypes, FixedPoint2> PainThresholds = new()
    {
        { PainThresholdTypes.PainFlinch, 10 },
        { PainThresholdTypes.Agony, 40 },
        { PainThresholdTypes.PainShock, 65 },
        { PainThresholdTypes.PainShockAndAgony, 85 },
    };

    /// <summary>
    /// Pre-sorted version of <see cref="PainThresholds"/> in descending order by value.
    /// </summary>
    public KeyValuePair<PainThresholdTypes, FixedPoint2>[]? SortedPainThresholds;

    /// <summary>
    /// Chance for a pain reflex to produce a scream sound.
    /// </summary>
    [DataField]
    public float ScreamChance = 0.2f;

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainScreams = new()
    {
        { Sex.Male, new SoundCollectionSpecifier("PainScreamsShortMale") },
        { Sex.Female, new SoundCollectionSpecifier("PainScreamsShortFemale") },
        { Sex.Unsexed, new SoundCollectionSpecifier("PainScreamsShortMale") },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> AgonyScreams = new()
    {
        { Sex.Male, new SoundCollectionSpecifier("AgonyScreamsMale") },
        { Sex.Female, new SoundCollectionSpecifier("AgonyScreamsFemale") },
        { Sex.Unsexed, new SoundCollectionSpecifier("AgonyScreamsMale") },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> PainShockScreams = new()
    {
        { Sex.Male, new SoundCollectionSpecifier("PainShockScreamsMale") },
        { Sex.Female, new SoundCollectionSpecifier("PainShockScreamsFemale") },
        { Sex.Unsexed, new SoundCollectionSpecifier("PainShockScreamsMale") },
    };

    [DataField]
    public Dictionary<Sex, SoundSpecifier> CritWhimpers = new()
    {
        { Sex.Male, new SoundCollectionSpecifier("CritWhimpersMale") },
        { Sex.Female, new SoundCollectionSpecifier("CritWhimpersFemale") },
        { Sex.Unsexed, new SoundCollectionSpecifier("CritWhimpersMale") },
    };

    [DataField]
    public SoundSpecifier PainRattles = new SoundCollectionSpecifier("PainRattles");

    [DataField]
    public TimeSpan CritScreamsIntervalMin = TimeSpan.FromSeconds(13f);

    [DataField]
    public TimeSpan CritScreamsIntervalMax = TimeSpan.FromSeconds(32f);

    /// <summary>
    /// Currently playing pain sounds, stopped when a new scream starts or the mob dies.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> PlayedPainScreams = new();
}
