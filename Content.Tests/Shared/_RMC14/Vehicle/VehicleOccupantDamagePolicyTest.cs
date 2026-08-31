using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using NUnit.Framework;

namespace Content.Tests.Shared._RMC14.Vehicle;

[TestFixture]
public sealed class VehicleOccupantDamagePolicyTest
{
    [Test]
    public void OrdinaryDamageDoesNotTransfer()
    {
        var settings = new VehicleOccupantDamageComponent();

        Assert.That(
            VehicleOccupantDamagePolicy.GetMultiplier(VehicleOccupantDamageKind.Ordinary, 100f, settings),
            Is.Zero);
    }

    [Test]
    public void StrongExplosionTransfersReducedDamage()
    {
        var settings = new VehicleOccupantDamageComponent();

        Assert.That(
            VehicleOccupantDamagePolicy.GetMultiplier(VehicleOccupantDamageKind.Explosion, 60f, settings),
            Is.EqualTo(0.1f));
    }

    [Test]
    public void LargeNearbyExplosionTransfersLessDamage()
    {
        var settings = new VehicleOccupantDamageComponent();

        Assert.That(
            VehicleOccupantDamagePolicy.GetMultiplier(VehicleOccupantDamageKind.Explosion, 30f, settings),
            Is.EqualTo(0.05f));
    }

    [Test]
    public void StructuralExplosionDamageDoesNotReachOccupantsOrIncreaseSeverity()
    {
        var settings = new VehicleOccupantDamageComponent();
        var explosionDamage = new DamageSpecifier
        {
            DamageDict =
            {
                ["Heat"] = 30,
                ["Structural"] = 300,
            },
        };

        var occupantDamage = VehicleOccupantDamagePolicy.GetLivingExplosionDamage(explosionDamage);

        Assert.Multiple(() =>
        {
            Assert.That(occupantDamage.DamageDict.ContainsKey("Structural"), Is.False);
            Assert.That(occupantDamage.GetTotal().Float(), Is.EqualTo(30f));
            Assert.That(
                VehicleOccupantDamagePolicy.GetMultiplier(
                    VehicleOccupantDamageKind.Explosion,
                    occupantDamage.GetTotal().Float(),
                    settings),
                Is.EqualTo(0.05f));
        });
    }

    [Test]
    public void HardCollisionTransfersReducedDamage()
    {
        var settings = new VehicleOccupantDamageComponent();

        Assert.That(
            VehicleOccupantDamagePolicy.GetMultiplier(VehicleOccupantDamageKind.Collision, 15f, settings),
            Is.EqualTo(0.1f));
    }

    [TestCase(VehicleOccupantDamageKind.Collision, 9.99f)]
    [TestCase(VehicleOccupantDamageKind.Explosion, 24.99f)]
    public void MinorImpactsDoNotTransfer(VehicleOccupantDamageKind kind, float damage)
    {
        var settings = new VehicleOccupantDamageComponent();

        Assert.That(VehicleOccupantDamagePolicy.GetMultiplier(kind, damage, settings), Is.Zero);
    }
}
