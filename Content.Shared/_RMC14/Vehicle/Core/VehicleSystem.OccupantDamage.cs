using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleSystem
{
    private static readonly ProtoId<DamageTypePrototype> OccupantCollisionDamageType = "Blunt";

    private readonly HashSet<EntityUid> _occupantDamageTargets = new();

    private void OnVehicleBeforeExplode(Entity<VehicleOccupantDamageComponent> ent, ref BeforeExplodeEvent args)
    {
        if (_net.IsClient)
            return;

        var damage = VehicleOccupantDamagePolicy.GetLivingExplosionDamage(args.Damage);
        var multiplier = VehicleOccupantDamagePolicy.GetMultiplier(
            VehicleOccupantDamageKind.Explosion,
            damage.GetTotal().Float(),
            ent.Comp);

        if (multiplier <= 0f)
            return;

        DamageOccupants(ent.Owner, damage * multiplier, DamageImpact.Explosion);
    }

    /// <summary>
    /// Applies the configured fraction of a hard collision's hull damage to everyone inside the vehicle.
    /// </summary>
    public void DamageOccupantsFromCollision(EntityUid vehicle, float hullDamage)
    {
        if (_net.IsClient ||
            !TryComp(vehicle, out VehicleOccupantDamageComponent? settings))
        {
            return;
        }

        var multiplier = VehicleOccupantDamagePolicy.GetMultiplier(
            VehicleOccupantDamageKind.Collision,
            hullDamage,
            settings);

        if (multiplier <= 0f)
            return;

        var damage = new DamageSpecifier
        {
            DamageDict =
            {
                [OccupantCollisionDamageType] = hullDamage * multiplier,
            },
        };

        DamageOccupants(vehicle, damage, DamageImpact.ForContact(damage));
    }

    private void DamageOccupants(EntityUid vehicle, DamageSpecifier damage, DamageImpact impact)
    {
        if (!TryComp(vehicle, out VehicleInteriorComponent? interior))
            return;

        _occupantDamageTargets.Clear();
        _occupantDamageTargets.UnionWith(interior.Passengers);
        _occupantDamageTargets.UnionWith(interior.Xenos);

        if (TryComp(vehicle, out VehicleComponent? vehicleComp) &&
            vehicleComp.Operator is { } operatorUid)
        {
            _occupantDamageTargets.Add(operatorUid);
        }

        foreach (var occupant in _occupantDamageTargets)
        {
            if (TerminatingOrDeleted(occupant))
                continue;

            _damageable.TryChangeDamage(occupant, damage, origin: vehicle, impact: impact);
        }

        _occupantDamageTargets.Clear();
    }
}
