using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle.Components;

namespace Content.IntegrationTests._RMC14.Vehicle;

[TestFixture]
[TestOf(typeof(VehicleOccupantDamageComponent))]
public sealed class VehicleOccupantDamagePrototypeTest : GameTest
{
    [TestCase("VehicleAPC")]
    [TestCase("VehicleBlackfoot")]
    [TestCase("VehicleBoxVan")]
    [TestCase("VehicleHumvee")]
    [TestCase("VehicleTank")]
    [TestCase("VehicleVan")]
    public async Task EnclosedVehiclesUseImpactOnlyOccupantDamage(string prototypeId)
    {
        await Server.WaitAssertion(() =>
        {
            var prototype = Server.ProtoMan.Index(prototypeId);
            var vehicle = (VehicleComponent)prototype.Components["Vehicle"].Component;

            Assert.Multiple(() =>
            {
                Assert.That(vehicle.TransferDamage, Is.False,
                    $"{prototypeId} must not transfer ordinary vehicle damage to its occupants.");
                Assert.That(prototype.Components.ContainsKey("VehicleOccupantDamage"), Is.True,
                    $"{prototypeId} must opt into impact-only occupant damage.");
            });
        });
    }
}
