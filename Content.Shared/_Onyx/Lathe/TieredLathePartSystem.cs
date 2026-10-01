// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Construction;
using Content.Shared.Lathe;
using Content.Shared.Materials;

namespace Content.Shared._Onyx.Lathe;

public sealed partial class TieredLathePartSystem : EntitySystem
{
    private const int DefaultBaseStorageLimit = 20000;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LatheComponent, MachinePartsChangedEvent>(OnPartsChanged);
        SubscribeLocalEvent<LatheComponent, MachineUpgradeExamineEvent>(OnUpgradeExamine);
    }

    private void OnPartsChanged(Entity<LatheComponent> ent, ref MachinePartsChangedEvent args)
    {
        if (!TryComp<TieredLathePartComponent>(ent, out var baseline))
        {
            if (args.Ratings.Count == 0)
                return;

            baseline = AddComp<TieredLathePartComponent>(ent);
        }

        if (baseline.BaseTimeMultiplier == 0f)
        {
            baseline.BaseTimeMultiplier = ent.Comp.TimeMultiplier;
            baseline.BaseMaterialUseMultiplier = ent.Comp.MaterialUseMultiplier;
        }

        var speed = Math.Clamp(1f - args.GetTierBonusSum(MachinePartKind.Servo) * 0.1f, 0.1f, 1f);
        ent.Comp.TimeMultiplier = baseline.BaseTimeMultiplier * MathF.Pow(speed, 0.8f);
        ent.Comp.MaterialUseMultiplier = baseline.BaseMaterialUseMultiplier;
        if (TryComp<MaterialStorageComponent>(ent, out var storage))
        {
            baseline.BaseStorageLimit ??= storage.StorageLimit ?? DefaultBaseStorageLimit;
            if (baseline.BaseStorageLimit is { } baseLimit)
            {
                storage.StorageLimit = Math.Max(baseLimit,
                    baseLimit + (int) MathF.Round(args.GetTierBonusSum(MachinePartKind.MatterBin) * 3750f));
                Dirty(ent, storage);
            }
        }
        Dirty(ent);
    }

    private void OnUpgradeExamine(Entity<LatheComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        if (!TryComp<TieredLathePartComponent>(ent, out var baseline))
            return;

        args.Add("lathe-component-upgrade-speed", baseline.BaseTimeMultiplier / ent.Comp.TimeMultiplier);
        if (baseline.BaseStorageLimit is { } baseLimit &&
            TryComp<MaterialStorageComponent>(ent, out var storage) &&
            storage.StorageLimit is { } storageLimit)
        {
            args.Add("machine-upgrade-capacity", (float) storageLimit / baseLimit);
        }
    }
}
