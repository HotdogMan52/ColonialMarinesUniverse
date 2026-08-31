using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

[RegisterComponent]
[Access(typeof(VehicleSystem))]
public sealed partial class VehicleOccupantDamageComponent : Component
{
    [DataField]
    public float CollisionDamageThreshold = 10f;

    [DataField]
    public float CollisionDamageMultiplier = 0.1f;

    /// <summary>
    /// Explosion damage at or above this value is treated as a direct or otherwise severe blast.
    /// </summary>
    [DataField]
    public float DirectExplosionDamageThreshold = 60f;

    [DataField]
    public float DirectExplosionDamageMultiplier = 0.1f;

    /// <summary>
    /// Blast falloff below this value is too weak to meaningfully reach the occupants.
    /// </summary>
    [DataField]
    public float NearbyExplosionDamageThreshold = 25f;

    [DataField]
    public float NearbyExplosionDamageMultiplier = 0.05f;
}

public enum VehicleOccupantDamageKind : byte
{
    Ordinary,
    Collision,
    Explosion,
}

public static class VehicleOccupantDamagePolicy
{
    private static readonly ProtoId<DamageTypePrototype> StructuralDamageType = "Structural";

    /// <summary>
    /// Keeps only positive damage that can meaningfully affect a living occupant.
    /// </summary>
    public static DamageSpecifier GetLivingExplosionDamage(DamageSpecifier damage)
    {
        var livingDamage = DamageSpecifier.GetPositive(damage);
        livingDamage.DamageDict.Remove(StructuralDamageType);
        return livingDamage;
    }

    public static float GetMultiplier(
        VehicleOccupantDamageKind kind,
        float damage,
        VehicleOccupantDamageComponent settings)
    {
        if (damage <= 0f)
            return 0f;

        return kind switch
        {
            VehicleOccupantDamageKind.Collision when damage >= settings.CollisionDamageThreshold =>
                MathF.Max(settings.CollisionDamageMultiplier, 0f),
            VehicleOccupantDamageKind.Explosion when damage >= settings.DirectExplosionDamageThreshold =>
                MathF.Max(settings.DirectExplosionDamageMultiplier, 0f),
            VehicleOccupantDamageKind.Explosion when damage >= settings.NearbyExplosionDamageThreshold =>
                MathF.Max(settings.NearbyExplosionDamageMultiplier, 0f),
            _ => 0f,
        };
    }
}
