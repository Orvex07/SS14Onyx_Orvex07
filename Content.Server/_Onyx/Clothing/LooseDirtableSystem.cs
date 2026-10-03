// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Clothing;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.Clothing;

public sealed partial class LooseDirtableSystem : EntitySystem
{
    private const float SoakInterval = 1f;
    private static readonly FixedPoint2 SoakTransferMultiplier = FixedPoint2.New(0.25f);

    [Dependency] private ClothingDirtSystem _dirt = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly HashSet<Entity<ClothingDirtableComponent>> _dirtables = [];
    private readonly HashSet<EntityUid> _soaking = [];
    private readonly HashSet<EntityUid> _initializedPuddles = [];
    private readonly List<EntityUid> _soakingBuffer = [];
    private float _soakAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleComponent, PuddleDirtChangedEvent>(OnPuddleChanged);
        SubscribeLocalEvent<ClothingDirtableComponent, MoveEvent>(OnDirtableMoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if ((_soakAccumulator += frameTime) < SoakInterval)
            return;

        _soakAccumulator %= SoakInterval;
        _soakingBuffer.Clear();
        _soakingBuffer.AddRange(_soaking);
        foreach (var uid in _soakingBuffer)
        {
            if (!TryComp(uid, out ClothingDirtableComponent? dirtable) ||
                _container.IsEntityInContainer(uid) ||
                !TryGetSource(uid, Transform(uid).Coordinates, out var source, out var solution))
            {
                _soaking.Remove(uid);
                continue;
            }

            TryApply((uid, dirtable), solution, source.Comp.TransferAmount * SoakTransferMultiplier);
        }

        _soakingBuffer.Clear();
        _soakingBuffer.AddRange(_initializedPuddles);
        foreach (var uid in _soakingBuffer)
        {
            if (!HasComp<PuddleComponent>(uid))
                _initializedPuddles.Remove(uid);
        }
    }

    private void OnPuddleChanged(Entity<PuddleComponent> ent, ref PuddleDirtChangedEvent args)
    {
        if (!_initializedPuddles.Add(ent))
            return;

        _dirtables.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 0.45f, _dirtables,
            LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var dirtable in _dirtables)
        {
            if (!_container.IsEntityInContainer(dirtable))
                _soaking.Add(dirtable);
        }
    }

    private void OnDirtableMoved(Entity<ClothingDirtableComponent> ent, ref MoveEvent args)
    {
        if (args.OnlyRotation)
            return;

        if (_container.IsEntityInContainer(ent) || !args.NewPosition.IsValid(EntityManager))
        {
            _soaking.Remove(ent);
            return;
        }

        if (TryGetSource(ent, args.NewPosition, out var source, out var solution))
        {
            if (_soaking.Add(ent))
                TryApply(ent, solution, source.Comp.TransferAmount * SoakTransferMultiplier);
            return;
        }

        _soaking.Remove(ent);
    }

    private void TryApply(Entity<ClothingDirtableComponent> ent, Solution source, FixedPoint2 transferAmount)
    {
        if (transferAmount <= 0)
            return;

        _dirt.TryDirtyClothing(ent, source, FixedPoint2.Min(source.Volume, transferAmount), ent.Comp);
    }

    private bool TryGetSource(EntityUid dirtable, EntityCoordinates coordinates,
        out Entity<SurfaceDirtSourceComponent> source, out Solution solution)
    {
        source = default;
        solution = default!;
        var xform = Transform(dirtable);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.CoordinatesToTile(gridUid, grid, coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp(uid, out SurfaceDirtSourceComponent? component) ||
                !_solutions.TryGetSolution(uid.Value, component.Solution, out _, out var foundSolution))
                continue;

            source = (uid.Value, component);
            solution = foundSolution;
            return true;
        }

        return false;
    }
}
