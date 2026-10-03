using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._Onyx.Clothing;

public sealed partial class ClothingDirtSystem : EntitySystem
{
    public const string DefaultSolutionName = "dirt";

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private readonly HashSet<EntityUid> _drying = new();
    private readonly List<EntityUid> _dryingBuffer = new();
    private float _dryUpdateAccumulator;
    private float _solutionUpdateAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingDirtableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ClothingDirtableComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ClothingDirtableComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ClothingDirtableComponent, SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<BodyPartComponent, MapInitEvent>(OnBodyPartMapInit);
        SubscribeLocalEvent<BloodstreamComponent, BleedingDirtEvent>(OnBleedingDirt,
            after: [typeof(WoundBleedingSystem)]);
    }

    private void OnShutdown(Entity<ClothingDirtableComponent> ent, ref ComponentShutdown args)
        => _drying.Remove(ent.Owner);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer)
            return;

        if ((_solutionUpdateAccumulator += frameTime) >= 1f)
        {
            _solutionUpdateAccumulator = 0f;
            // Nested Solution edits in VV do not dirty their SolutionComponent.
            RefreshExternallyModifiedSolutions();
        }

        if ((_dryUpdateAccumulator += frameTime) < 5f)
            return;

        var elapsed = _dryUpdateAccumulator;
        _dryUpdateAccumulator = 0f;
        _dryingBuffer.Clear();
        _dryingBuffer.AddRange(_drying);
        foreach (var uid in _dryingBuffer)
        {
            if (!TryComp(uid, out ClothingDirtableComponent? dirtable))
            {
                _drying.Remove(uid);
                continue;
            }

            dirtable.DryAccumulator += elapsed;
            if (dirtable.DryAccumulator < dirtable.DryInterval)
                continue;
            dirtable.DryAccumulator %= dirtable.DryInterval;
            DryClothing((uid, dirtable));
        }
    }

    private void OnMapInit(Entity<ClothingDirtableComponent> ent, ref MapInitEvent args)
    {
        if (_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEnt, out var solution))
        {
            if (solution.MaxVolume != ent.Comp.Capacity)
                _solutions.SetCapacity(solutionEnt.Value, ent.Comp.Capacity);
            Refresh(ent, solution);
        }
    }

    private void OnBodyPartMapInit(Entity<BodyPartComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsServer)
            return;
        var dirtable = EnsureComp<ClothingDirtableComponent>(ent);
        _solutions.EnsureSolution(ent.Owner, dirtable.Solution, out var solutionEnt);
        _solutions.SetCapacity(solutionEnt, dirtable.Capacity);
        Refresh((ent.Owner, dirtable), solutionEnt.Comp.Solution);
    }

    private void OnSolutionChanged(Entity<ClothingDirtableComponent> ent, ref SolutionChangedEvent args)
    {
        if (!_net.IsServer || args.Solution.Comp.Id != ent.Comp.Solution)
            return;
        Refresh(ent, args.Solution.Comp.Solution);
    }

    private void OnExamined(Entity<ClothingDirtableComponent> ent, ref ExaminedEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) || solution.Volume <= 0 ||
            solution.GetPrimaryReagentId() is not { } primaryId ||
            !_prototype.Resolve<ReagentPrototype>(primaryId.Prototype, out var primary))
            return;

        args.PushMarkup(Loc.GetString("clothing-dirtable-examine",
            ("color", solution.GetColor(_prototype).ToHexNoAlpha()),
            ("desc", primary.LocalizedPhysicalDescription),
            ("chemCount", solution.Contents.Count)));
    }

    private void OnBleedingDirt(Entity<BloodstreamComponent> body, ref BleedingDirtEvent args)
    {
        if (!args.Handled)
            TryDirtyBody(body, args.Source, args.Amount, DirtExposure.FullBody);
    }

    public bool TryDirtyClothing(EntityUid clothing, Solution source, FixedPoint2 amount,
        ClothingDirtableComponent? component = null)
    {
        if (!_net.IsServer || amount <= 0 || source.Volume <= 0 ||
            !Resolve(clothing, ref component, false) ||
            !_solutions.TryGetSolution(clothing, component.Solution, out var solutionEnt, out var dirt))
            return false;

        var target = FixedPoint2.Min(amount, source.Volume, dirt.AvailableVolume);
        if (target <= 0)
            return false;

        var sample = new Solution();
        var sourceVolume = source.Volume;
        foreach (var reagent in source.Contents)
        {
            var accepted = FixedPoint2.Min(
                reagent.Quantity / sourceVolume * target,
                component.MaxReagentAmount - dirt.GetReagentQuantity(reagent.Reagent),
                target - sample.Volume);
            if (accepted > 0)
                sample.AddReagent(reagent.Reagent, accepted);
        }

        if (sample.Volume <= 0 || !_solutions.TryAddSolution(solutionEnt.Value, sample))
            return false;
        if (ProcessCleaners(dirt))
            _solutions.UpdateChemicals(solutionEnt.Value);
        return true;
    }

    public bool TryAddCleanerToClothing(EntityUid clothing, ReagentId cleaner, FixedPoint2 amount,
        ClothingDirtableComponent? component = null)
    {
        if (!_net.IsServer || amount <= 0 || !Resolve(clothing, ref component, false) ||
            !_prototype.Resolve<ReagentPrototype>(cleaner.Prototype, out var prototype) ||
            GetCleanMultiplier(prototype) <= 0 ||
            !_solutions.TryGetSolution(clothing, component.Solution, out var solutionEnt, out var dirt))
            return false;

        var add = FixedPoint2.Min(amount,
            component.MaxReagentAmount - dirt.GetReagentQuantity(cleaner));
        if (add <= 0)
            return false;

        var spaceNeeded = FixedPoint2.Max(FixedPoint2.Zero, add - dirt.AvailableVolume);
        if (spaceNeeded > 0)
            RemoveWashableDirt(dirt, spaceNeeded, GetWashableVolume(dirt));

        add = FixedPoint2.Min(add, dirt.AvailableVolume);
        if (add <= 0)
            return false;

        var solution = new Solution();
        solution.AddReagent(cleaner, add);
        if (!_solutions.TryAddSolution(solutionEnt.Value, solution))
            return false;

        if (ProcessCleaners(dirt))
            _solutions.UpdateChemicals(solutionEnt.Value);
        return true;
    }

    public bool TryWashClothing(EntityUid clothing, ReagentId cleaner, FixedPoint2 amount,
        ClothingDirtableComponent? component = null, Solution? runoff = null)
    {
        if (!_net.IsServer || amount <= 0 || !Resolve(clothing, ref component, false) ||
            !_prototype.Resolve<ReagentPrototype>(cleaner.Prototype, out var prototype) ||
            !_solutions.TryGetSolution(clothing, component.Solution, out var solutionEnt, out var dirt))
            return false;

        var multiplier = GetCleanMultiplier(prototype);
        if (multiplier <= 0)
            return false;

        var washable = GetWashableVolume(dirt);
        var remaining = FixedPoint2.Min(amount * multiplier, washable);
        if (remaining <= 0)
            return true;

        var removed = RemoveWashableDirt(dirt, remaining, washable, runoff);

        _solutions.UpdateChemicals(solutionEnt.Value);
        return removed > 0;
    }

    public bool TryCleanDirt(EntityUid dirtable, float amount, ClothingDirtableComponent? component = null)
    {
        if (!_net.IsServer || amount <= 0 || !Resolve(dirtable, ref component, false) ||
            !_solutions.TryGetSolution(dirtable, component.Solution, out var solutionEnt, out var dirt))
            return false;

        var washable = GetWashableVolume(dirt);
        var removed = RemoveWashableDirt(dirt, FixedPoint2.New(amount), washable);
        if (removed <= 0)
            return false;
        _solutions.UpdateChemicals(solutionEnt.Value);
        return true;
    }

    public bool TryDirtyWornSplash(EntityUid wearer, Solution source, FixedPoint2 amount)
        => TryDirtyBody(wearer, source, amount, DirtExposure.Splash);

    public bool TryDirtyWornPuddleStep(EntityUid wearer, Solution source, FixedPoint2 amount)
    {
        return TryDirtyBody(wearer, source, amount, DirtExposure.Ground);
    }

    public bool TryDirtyWornPuddleCrawl(EntityUid wearer, Solution source, FixedPoint2 amount)
    {
        return TryDirtyBody(wearer, source, amount, DirtExposure.Crawl, false);
    }

    public bool TryDirtyBody(EntityUid body, Solution source, FixedPoint2 amount, DirtExposure exposure, bool splitAmount = true)
    {
        var targetCount = 0;
        foreach (var part in _body.GetBodyChildren(body))
        {
            if (part.Component.DirtExposures.Contains(exposure))
                targetCount++;
        }
        if (targetCount == 0)
            return false;

        var clothingTargets = new HashSet<EntityUid>();
        _inventory.TryGetSlots(body, out var definitions);
        var amountPerPart = splitAmount ? amount / targetCount : amount;
        var changed = false;
        foreach (var (part, component) in _body.GetBodyChildren(body))
        {
            if (!component.DirtExposures.Contains(exposure))
                continue;

            changed |= TryDirtyClothing(part, source, amountPerPart);
            if (definitions != null)
                AddCoveredClothing(body, component, definitions, clothingTargets);
        }

        var clothingAmount = amount / 2;
        var amountPerClothing = splitAmount && clothingTargets.Count > 0
            ? clothingAmount / clothingTargets.Count
            : clothingAmount;
        foreach (var clothing in clothingTargets)
            changed |= TryDirtyClothing(clothing, source, amountPerClothing);
        return changed;
    }

    public bool TryDirtyBodyPart(EntityUid body, EntityUid part, Solution source, FixedPoint2 amount)
    {
        var changed = TryDirtyClothing(part, source, amount);
        if (!TryComp(part, out BodyPartComponent? component) ||
            !_inventory.TryGetSlots(body, out var definitions))
            return changed;

        var clothingTargets = new HashSet<EntityUid>();
        AddCoveredClothing(body, component, definitions, clothingTargets);
        if (clothingTargets.Count == 0)
            return changed;

        var amountPerClothing = amount / 2 / clothingTargets.Count;
        foreach (var clothing in clothingTargets)
            changed |= TryDirtyClothing(clothing, source, amountPerClothing);
        return changed;
    }

    public bool TryWashBody(EntityUid body, ReagentId cleaner, FixedPoint2 amount, DirtExposure exposure,
        Solution? runoff = null)
    {
        var changed = false;
        foreach (var (part, component) in _body.GetBodyChildren(body))
        {
            if (!component.DirtExposures.Contains(exposure))
                continue;
            changed |= TryWashClothing(part, cleaner, amount, runoff: runoff);
        }
        return changed;
    }

    private void AddCoveredClothing(EntityUid body, BodyPartComponent part, SlotDefinition[] definitions,
        HashSet<EntityUid> targets)
    {
        foreach (var definition in definitions)
        {
            var flags = definition.SlotFlags;
            var parentName = definition.SubSlotOf;
            for (var depth = 0; parentName != null && depth < definitions.Length; depth++)
            {
                if (!_inventory.TryGetSlot(body, parentName, out var parent))
                    break;
                flags |= parent.SlotFlags;
                parentName = parent.SubSlotOf;
            }

            foreach (var layer in part.DirtCoverageLayers)
            {
                if ((flags & layer) == 0)
                    continue;

                if (_inventory.TryGetSlotEntity(body, definition.Name, out var item))
                    targets.Add(item.Value);
                break;
            }
        }
    }

    private void DryClothing(Entity<ClothingDirtableComponent> ent)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solutionEnt, out var dirt))
        {
            _drying.Remove(ent.Owner);
            return;
        }

        var changed = ProcessCleaners(dirt);
        foreach (var reagent in dirt.Contents.ToArray())
        {
            if (!_prototype.Resolve<ReagentPrototype>(reagent.Reagent.Prototype, out var prototype) ||
                prototype.EvaporationSpeed <= 0)
                continue;

            var remove = FixedPoint2.Min(prototype.EvaporationSpeed, reagent.Quantity);
            if (remove > 0)
                changed |= dirt.RemoveReagent(reagent.Reagent, remove) > 0;
        }

        if (changed)
            _solutions.UpdateChemicals(solutionEnt.Value);
    }

    private bool ProcessCleaners(Solution dirt)
    {
        var changed = false;
        foreach (var cleaner in dirt.Contents.ToArray())
        {
            if (!_prototype.Resolve<ReagentPrototype>(cleaner.Reagent.Prototype, out var prototype))
                continue;

            var multiplier = GetCleanMultiplier(prototype);
            if (multiplier <= 0)
                continue;

            var washable = GetWashableVolume(dirt);
            if (washable <= 0)
                break;

            var cleanAmount = FixedPoint2.Min(
                cleaner.Quantity * multiplier,
                washable);
            var removed = RemoveWashableDirt(dirt, cleanAmount, washable);
            if (removed <= 0)
                continue;

            var toConsume = FixedPoint2.Min(cleaner.Quantity, removed / multiplier);
            if (toConsume <= 0)
                toConsume = FixedPoint2.Min(cleaner.Quantity, FixedPoint2.Epsilon);
            if (toConsume > 0)
                dirt.RemoveReagent(cleaner.Reagent, toConsume);
            changed = true;
        }

        return changed;
    }

    private FixedPoint2 RemoveWashableDirt(Solution dirt, FixedPoint2 amount, FixedPoint2 washable,
        Solution? runoff = null)
    {
        var remaining = FixedPoint2.Min(amount, washable);
        if (remaining <= 0)
            return FixedPoint2.Zero;

        var removed = FixedPoint2.Zero;
        var original = remaining;
        foreach (var reagent in dirt.Contents.ToArray())
        {
            if (remaining <= 0 || IsCleaner(reagent.Reagent))
                continue;
            var quantity = FixedPoint2.Min(reagent.Quantity / washable * original, remaining);
            if (quantity <= 0)
                quantity = FixedPoint2.Min(reagent.Quantity, remaining);
            var current = dirt.RemoveReagent(reagent.Reagent, quantity, preserveOrder: true);
            if (current > 0)
            {
                runoff?.AddReagent(reagent.Reagent, current);
                removed += current;
            }
            remaining -= current;
        }

        foreach (var reagent in dirt.Contents.ToArray())
        {
            if (remaining <= 0)
                break;
            if (IsCleaner(reagent.Reagent))
                continue;
            var current = dirt.RemoveReagent(reagent.Reagent, remaining, preserveOrder: true);
            if (current <= 0)
                continue;
            runoff?.AddReagent(reagent.Reagent, current);
            removed += current;
            remaining -= current;
        }

        return removed;
    }

    private FixedPoint2 GetWashableVolume(Solution dirt)
    {
        var washable = FixedPoint2.Zero;
        foreach (var reagent in dirt.Contents)
        {
            if (!IsCleaner(reagent.Reagent))
                washable += reagent.Quantity;
        }
        return washable;
    }

    private bool IsCleaner(ReagentId reagent)
        => _prototype.Resolve<ReagentPrototype>(reagent.Prototype, out var prototype) &&
           GetCleanMultiplier(prototype) > 0;

    private static float GetCleanMultiplier(ReagentPrototype prototype)
    {
        if (prototype.ReactiveEffects == null)
            return 0f;
        foreach (var entry in prototype.ReactiveEffects.Values)
        {
            if (!entry.Methods.Contains(ReactionMethod.Touch))
                continue;
            foreach (var effect in entry.Effects)
            {
                if (effect is CleanDirt clean)
                    return clean.Multiplier;
            }
        }
        return 0f;
    }

    private void Refresh(Entity<ClothingDirtableComponent> ent, Solution dirt)
    {
        ent.Comp.SolutionHash = GetSolutionHash(dirt, out ent.Comp.SolutionContentsHash);
        var dryable = false;
        var visibleVolume = FixedPoint2.Zero;
        var colorVolume = FixedPoint2.Zero;
        var mixedColor = default(Color);
        var firstColor = true;
        foreach (var reagent in dirt.Contents)
        {
            _prototype.Resolve<ReagentPrototype>(reagent.Reagent.Prototype, out var prototype);
            dryable |= prototype != null && prototype.EvaporationSpeed > 0 && reagent.Quantity > 0;
            if (prototype != null && GetCleanMultiplier(prototype) > 0)
                continue;

            visibleVolume += reagent.Quantity;
            colorVolume += reagent.Quantity;
            if (prototype == null)
                continue;

            if (firstColor)
            {
                firstColor = false;
                mixedColor = prototype.SubstanceColor;
                continue;
            }

            mixedColor = Color.InterpolateBetween(mixedColor,
                prototype.SubstanceColor,
                reagent.Quantity.Float() / colorVolume.Float());
        }
        if (_net.IsServer)
        {
            if (dryable) _drying.Add(ent.Owner);
            else _drying.Remove(ent.Owner);
        }

        Color? color = null;
        var visualCapacity = FixedPoint2.Min(ent.Comp.Capacity, ent.Comp.MaxReagentAmount);
        if (visibleVolume > 0 && visualCapacity > 0)
        {
            var alpha = Math.Clamp(visibleVolume.Float() / visualCapacity.Float(),
                ent.Comp.MinVisualCoverage, 1f);
            color = mixedColor.WithAlpha(alpha);
        }
        if (ent.Comp.DirtColor == color)
            return;
        ent.Comp.DirtColor = color;
        Dirty(ent);
        _item.VisualsChanged(ent.Owner);
    }

    private void RefreshExternallyModifiedSolutions()
    {
        var query = EntityQueryEnumerator<ClothingDirtableComponent>();
        while (query.MoveNext(out var uid, out var dirtable))
        {
            if (!_solutions.TryGetSolution(uid, dirtable.Solution, out var solutionEnt, out var dirt))
                continue;

            var solutionHash = GetSolutionHash(dirt, out var contentsHash);
            if (dirtable.SolutionHash == solutionHash)
                continue;

            if (dirtable.SolutionContentsHash != contentsHash)
            {
                var volume = FixedPoint2.Zero;
                for (var i = dirt.Contents.Count - 1; i >= 0; i--)
                {
                    if (dirt.Contents[i].Quantity <= 0)
                    {
                        dirt.Contents.RemoveAt(i);
                        continue;
                    }

                    volume += dirt.Contents[i].Quantity;
                }
                dirt.Volume = volume;
            }
            _solutions.UpdateChemicals(solutionEnt.Value);
        }
    }

    private static int GetSolutionHash(Solution solution, out int contentsHash)
    {
        var contents = new HashCode();
        foreach (var reagent in solution.Contents)
        {
            contents.Add(reagent.Reagent);
            contents.Add(reagent.Quantity);
        }
        contentsHash = contents.ToHashCode();

        var hash = new HashCode();
        hash.Add(solution.Volume);
        hash.Add(solution.MaxVolume);
        hash.Add(solution.Temperature);
        hash.Add(solution.CanReact);
        hash.Add(contentsHash);
        return hash.ToHashCode();
    }

}
