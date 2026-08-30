using Content.Client.GameStates;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameStates;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(ClientFullStateResetGuard))]
public sealed class ClientFullStateResetGuardTest : GameTest
{
#if DEBUG
    [Test]
    public async Task PendingFullStateRemovesNestedStaleEntitiesWithoutBreakingTheTick()
    {
        var map = await Pair.CreateTestMap();
        EntityUid serverParent = default;
        NetEntity parentNet = default;
        NetEntity childNet = default;

        await Server.WaitAssertion(() =>
        {
            serverParent = SEntMan.SpawnEntity(null, map.GridCoords);
            var serverChild = SEntMan.SpawnEntity(null, map.GridCoords);
            Server.System<SharedTransformSystem>().SetParent(serverChild, serverParent);
            parentNet = SEntMan.GetNetEntity(serverParent);
            childNet = SEntMan.GetNetEntity(serverChild);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(parentNet, out var parent), Is.True);
            Assert.That(CEntMan.TryGetEntity(childNet, out var child), Is.True);
            Assert.That(CEntMan.GetComponent<TransformComponent>(child!.Value).ParentUid, Is.EqualTo(parent));
        });

        var gameStates = (ClientGameStateManager) Client.ResolveDependency<IClientGameStateManager>();
        gameStates.DropStates = true;
        try
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(serverParent));
            await Pair.RunTicksSync(5);

            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.TryGetEntity(parentNet, out _), Is.True, "the deletion state should have been dropped");
                Assert.That(CEntMan.TryGetEntity(childNet, out _), Is.True, "the nested child should still be stale locally");
                gameStates.DropStates = false;
                gameStates.RequestFullState();
            });

            await Pair.RunTicksSync(20);

            await Client.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(CEntMan.TryGetEntity(parentNet, out _), Is.False);
                    Assert.That(CEntMan.TryGetEntity(childNet, out _), Is.False);
                });
            });
        }
        finally
        {
            gameStates.DropStates = false;
        }
    }
#endif

    [Test]
    public async Task PreparationSnapshotsChildrenBeforeDetachingThem()
    {
        await Client.WaitAssertion(() =>
        {
            var parent = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var child = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            Client.System<SharedTransformSystem>().SetParent(child, parent);

            Assert.That(CEntMan.GetComponent<TransformComponent>(parent).ChildCount, Is.EqualTo(1));

            var guard = Client.ResolveDependency<ClientFullStateResetGuard>();
            guard.PrepareStaleEntity(parent);

            Assert.Multiple(() =>
            {
                Assert.That(CEntMan.Deleted(parent), Is.False, "the engine still owns deletion of the stale parent");
                Assert.That(CEntMan.Deleted(child), Is.True, "client-only children follow PartialStateReset semantics");
                Assert.That(CEntMan.GetComponent<TransformComponent>(parent).ChildCount, Is.Zero);
            });

            var lateChild = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            Client.System<SharedTransformSystem>().SetParent(lateChild, parent);
            guard.PrepareStaleEntity(parent);

            Assert.Multiple(() =>
            {
                Assert.That(CEntMan.Deleted(lateChild), Is.True, "a child attached after the first pass must also be prepared");
                Assert.That(CEntMan.GetComponent<TransformComponent>(parent).ChildCount, Is.Zero);
            });

            CEntMan.DeleteEntity(parent);
        });
    }
}
