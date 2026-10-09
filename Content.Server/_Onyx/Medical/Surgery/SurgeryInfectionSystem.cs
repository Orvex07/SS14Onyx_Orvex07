using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Disease.Components;
using Content.Shared._Onyx.Disease.Systems;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class SurgeryInfectionSystem : EntitySystem
{
    private static readonly EntProtoId SurgicalInfection = "DiseaseSurgicalSiteInfection";
    private static readonly TimeSpan InfectionAttemptCooldown = TimeSpan.FromSeconds(30);

    private const float BaseInfectionChance = 0.7f;
    private const float MinimumComplexity = 12f;
    private const float MaximumComplexity = 24f;

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private SharedDiseaseSystem _disease = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public void OnStep(ref SurgeryStepEvent args)
    {
        // Mechanical targets cannot develop surgical site infections.
        if (TryComp(args.Body, out SurgeryTargetComponent? surgeryTarget) && surgeryTarget.InfectionImmune)
            return;

        // The patient's own blood cannot infect them: it never raises the chance directly.
        // It only marks the gear, which then threatens OTHER patients (cross-contamination).
        SoilFromBleeding(args.User, args.Body, args.Part);

        var protection = GetSterility(args.User, args.Body);

        // 100% sterility or higher rules out infection entirely.
        if (protection >= SurgerySterility.FullProtection)
            return;

        var chance = BaseInfectionChance * (1f - Math.Max(0f, protection) / SurgerySterility.FullProtection);
        TryInfect(args.Body, chance);
    }

    /// <summary>
    /// Sums sterility points of the surgeon's worn gear for the given patient.
    /// Gear stained with someone else's blood grants nothing and penalizes the total;
    /// the patient's own blood on it is harmless to them.
    /// Missing gloves penalize the total as bare hands.
    /// </summary>
    public float GetSterility(EntityUid surgeon, EntityUid patient)
    {
        var protection = 0f;
        var patientNet = GetNetEntity(patient);

        var slots = _inventory.GetSlotEnumerator(surgeon);
        while (slots.NextItem(out var item, out _))
        {
            if (!TryComp<SurgeryInfectionProtectionComponent>(item, out var gear))
                continue;

            if (TryComp<SurgerySoiledComponent>(item, out var soiled) &&
                (soiled.Source == null || soiled.Source != patientNet))
                protection -= SurgerySterility.SoiledPenalty;
            else
                protection += gear.Protection;
        }

        if (!HasGloves(surgeon))
            protection -= SurgerySterility.BareHandsPenalty;

        return protection;
    }

    private bool HasGloves(EntityUid surgeon) =>
        _inventory.TryGetSlotEntity(surgeon, "gloves", out _);

    /// <summary>
    /// Dirt bridge: working a bleeding part stains the surgeon's gloves
    /// with that patient's blood. Stained gear must be replaced before
    /// operating on anyone else.
    /// </summary>
    public void SoilFromBleeding(EntityUid surgeon, EntityUid body, EntityUid part)
    {
        if (!IsPartBleeding(part))
            return;

        if (_inventory.TryGetSlotEntity(surgeon, "gloves", out var gloves) && gloves.HasValue)
        {
            var soiled = EnsureComp<SurgerySoiledComponent>(gloves.Value);
            soiled.Source = GetNetEntity(body);
            Dirty(gloves.Value, soiled);
        }
    }

    /// <summary>
    /// A part bleeds if it or any organ inside it has an active bleeding rate.
    /// Wounds live on organs, so checking the part alone is not enough.
    /// </summary>
    private bool IsPartBleeding(EntityUid part)
    {
        if (_bleeding.GetPartRate(part) > 0f)
            return true;

        foreach (var (organId, _) in _body.GetPartOrgans(part))
        {
            if (_bleeding.GetPartRate(organId) > 0f)
                return true;
        }

        return false;
    }

    private void TryInfect(EntityUid body, float chance)
    {
        if (TryComp<DiseaseCarrierComponent>(body, out var carrier))
        {
            foreach (var diseaseUid in carrier.Diseases.ContainedEntities)
            {
                if (HasComp<SurgicalSiteInfectionComponent>(diseaseUid))
                    return;
            }
        }

        var cooldown = EnsureComp<SurgeryInfectionCooldownComponent>(body);
        if (_timing.CurTime < cooldown.NextAttempt)
            return;

        cooldown.NextAttempt = _timing.CurTime + InfectionAttemptCooldown;
        if (!_random.Prob(chance))
            return;

        var complexity = _random.NextFloat(MinimumComplexity, MaximumComplexity);
        var disease = _disease.MakeRandomDisease(SurgicalInfection, complexity, 0.2f);
        if (disease != null && !_disease.TryInfect(body, disease.Value))
            QueueDel(disease.Value);
    }
}
