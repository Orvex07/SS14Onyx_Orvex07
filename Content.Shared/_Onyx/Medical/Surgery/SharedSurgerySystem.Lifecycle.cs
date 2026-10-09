using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.GameTicking;
using Content.Shared.Standing;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    public override void Initialize()
    {
        base.Initialize();
        InitializeLifecycle();
        InitializePipeline();
        InitializeConditions();
        InitializeTools();
        InitializeBodyParts();
        InitializeOrgans();
        InitializeCavity();
        InitializeComponents();
        InitializeDamage();
        InitializeStatusEffects();
        LoadSurgeryPrototypes();
    }

    private void InitializeLifecycle()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<BodyPartComponent, ComponentStartup>(OnBodyPartStartup);
        SubscribeLocalEvent<BodyPartComponent, ComponentShutdown>(OnBodyPartShutdown);
        SubscribeLocalEvent<SurgeryTargetComponent, ComponentStartup>(OnSurgeryTargetStartup);
        SubscribeLocalEvent<SurgeryTargetComponent, ComponentShutdown>(OnSurgeryTargetShutdown);
        SubscribeLocalEvent<SurgeryTargetComponent, StandAttemptEvent>(OnTargetStandAttempt);
        SubscribeLocalEvent<SurgeryTargetComponent, AccessibleOverrideEvent>(OnTargetAccessible);
    }

    // Detached parts are standalone surgery targets, so transplant surgery can work on them.
    private void OnBodyPartStartup(Entity<BodyPartComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<SurgeryTargetComponent>(ent);
    }

    private void OnSurgeryTargetStartup(Entity<SurgeryTargetComponent> ent, ref ComponentStartup args)
    {
        var ui = EnsureComp<UserInterfaceComponent>(ent);
        _ui.SetUi((ent.Owner, ui), SurgeryUIKey.Key, new InterfaceData("SurgeryBoundUserInterface"));
    }

    private void OnBodyPartShutdown(Entity<BodyPartComponent> ent, ref ComponentShutdown args)
    {
        RemoveSurgerySites(ent.Owner);
    }

    private void OnSurgeryTargetShutdown(Entity<SurgeryTargetComponent> ent, ref ComponentShutdown args)
    {
        RemoveSurgerySites(ent.Owner);
    }

    private void RemoveSurgerySites(EntityUid entity)
    {
        foreach (var site in ActiveSurgerySites.Keys.ToArray())
        {
            if (site.Body == entity || site.Part == entity)
                ActiveSurgerySites.Remove(site);
        }
    }

    private void OnTargetAccessible(Entity<SurgeryTargetComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Accessible || args.Target != ent.Owner ||
            !TryComp(ent, out BodyPartComponent? part) || part.Body is not { } body ||
            !_interaction.CanAccess(args.User, body))
            return;

        args.Accessible = true;
        args.Handled = true;
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        foreach (var (site, active) in ActiveSurgerySites.ToArray())
        {
            if (site.Body == args.Entity.Owner || site.Part == args.Entity.Owner || active.User == args.Entity.Owner)
                ActiveSurgerySites.Remove(site);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsServer)
            ProcessPendingSurgeryRepeats();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _singletons.Clear();
        ActiveSurgerySites.Clear();
        _pendingSurgeryRepeats.Clear();
        _processingSurgeryRepeats.Clear();
    }

    protected virtual void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        ClearSingletons();
        LoadSurgeryPrototypes();
    }

    protected void ClearSingletons()
    {
        foreach (var singleton in _singletons.Values)
            QueueDel(singleton);

        _singletons.Clear();
    }
}
