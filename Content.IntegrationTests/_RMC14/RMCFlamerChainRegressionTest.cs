using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(SharedRMCFlamerSystem))]
public sealed class RMCFlamerChainRegressionTest : GameTest
{
    private static readonly EntProtoId TestFire = "RMCFlamerChainTestFire";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RMCFlamerChainTestFire
        """;

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task OverdueFlamesSpreadFromOrigin()
    {
        var map = await Pair.CreateTestMap();
        EntityUid chain = default;
        var positions = new[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(2, 0),
        };

        await Server.WaitPost(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(2, 0), map.Tile.Tile);

            chain = SEntMan.SpawnEntity(null, map.GridCoords);
            var chainComponent = SEntMan.EnsureComponent<RMCFlamerChainComponent>(chain);
            var overdue = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
#pragma warning disable RA0002 // Test setup needs to seed the system-owned flame queue.
            chainComponent.Spawn = TestFire;
            chainComponent.Tiles.AddRange(positions
                .Select(position => new LineTile(new MapCoordinates(position, map.MapId), overdue))
                .ToList());
#pragma warning restore RA0002
        });

        await Server.WaitRunTicks(1);

        await Server.WaitAssertion(() =>
        {
            var chainComponent = SEntMan.GetComponent<RMCFlamerChainComponent>(chain);
            var remaining = chainComponent.Tiles.Select(tile => tile.Coordinates.Position);
            var fire = SEntMan.EntityQuery<MetaDataComponent>()
                .Single(metadata => metadata.EntityPrototype?.ID == TestFire);
            var firePosition = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(fire.Owner).Position;

            Assert.Multiple(() =>
            {
                Assert.That(remaining, Is.EqualTo(positions[1..]));
                Assert.That(firePosition, Is.EqualTo(positions[0]));
            });
        });
    }
}
