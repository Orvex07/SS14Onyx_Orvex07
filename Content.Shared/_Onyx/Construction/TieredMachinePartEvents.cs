// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Construction;

public sealed class MachinePartsChangedEvent : EntityEventArgs
{
    public readonly IReadOnlyDictionary<MachinePartKind, MachinePartRating> Ratings;

    public MachinePartsChangedEvent(IReadOnlyDictionary<MachinePartKind, MachinePartRating> ratings)
    {
        Ratings = ratings;
    }

    public float GetRating(MachinePartKind kind)
    {
        return Ratings.TryGetValue(kind, out var rating) ? rating.Average : 1f;
    }

    public float GetRatingSum(MachinePartKind kind)
    {
        return Ratings.TryGetValue(kind, out var rating) ? rating.Sum : 0f;
    }

    public float GetTierBonusSum(MachinePartKind kind)
    {
        var rating = GetRating(kind);
        var sum = GetRatingSum(kind);
        return rating > 0f ? sum * (rating - 1f) / rating : 0f;
    }
}

public readonly record struct MachinePartRating(float Average, float Sum);

public sealed class MachineUpgradeExamineEvent : EntityEventArgs
{
    public readonly List<MachinePartExamineEntry> Parts = [];
    public readonly List<MachineUpgradeExamineEntry> Upgrades = [];

    public void Add(LocId name, float multiplier)
    {
        Upgrades.Add(new MachineUpgradeExamineEntry(name, multiplier, true));
    }

    public void AddValue(LocId name, float value)
    {
        Upgrades.Add(new MachineUpgradeExamineEntry(name, value, false));
    }

    public void AddPart(MachinePartKind kind, int tier, int amount)
    {
        Parts.Add(new MachinePartExamineEntry(kind, tier, amount));
    }
}
