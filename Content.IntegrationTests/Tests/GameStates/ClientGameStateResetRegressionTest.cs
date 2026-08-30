using System.Linq;
using Content.Client.Lobby;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Client.GameStates;
using Robust.Client.State;
using Robust.Client.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using ServerPvsOverrideSystem = Robust.Server.GameStates.PvsOverrideSystem;

namespace Content.IntegrationTests.Tests.GameStates;

[TestFixture]
public sealed class ClientGameStateResetRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [Test]
    public async Task RoundCleanupPreservesStandaloneClientEntities()
    {
        await Client.WaitAssertion(() =>
        {
            var entityManager = Client.EntMan;
            var clientEntity = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entityManager.EventBus.RaiseEvent(EventSource.Network, new RoundRestartCleanupEvent());
                Assert.That(entityManager.EntityExists(clientEntity), Is.True,
                    "Round cleanup must preserve standalone client-only entities.");
            }
            finally
            {
                if (entityManager.EntityExists(clientEntity))
                    entityManager.DeleteEntity(clientEntity);
            }
        });
    }

    [Test]
    public async Task NetworkCleanupPreventsFullStateResetEnumerationInvalidation()
    {
        NetEntity firstNetworkEntity = default;
        NetEntity secondNetworkEntity = default;

        await Server.WaitAssertion(() =>
        {
            var pvsOverride = Server.System<ServerPvsOverrideSystem>();
            var first = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var second = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            firstNetworkEntity = SEntMan.GetNetEntity(first);
            secondNetworkEntity = SEntMan.GetNetEntity(second);
            pvsOverride.AddSessionOverride(first, ServerSession!);
            pvsOverride.AddSessionOverride(second, ServerSession!);
        });
        await Pair.RunUntilSynced();

        try
        {
            await Client.WaitAssertion(() =>
            {
                var entityManager = Client.EntMan;
                var gameStateManager = Client.ResolveDependency<IClientGameStateManager>();
                var timing = Client.ResolveDependency<IClientGameTiming>();
                var probeSystem = Client.System<FullStateResetReentryProbeSystem>();
                Assert.That(entityManager.TryGetEntity(firstNetworkEntity, out var firstClientEntity), Is.True);
                Assert.That(entityManager.TryGetEntity(secondNetworkEntity, out var secondClientEntity), Is.True);
                entityManager.AddComponent<FullStateResetReentryProbeComponent>(firstClientEntity!.Value);
                entityManager.AddComponent<FullStateResetReentryProbeComponent>(secondClientEntity!.Value);

                var entities = entityManager.EntityQuery<MetaDataComponent>()
                    .Where(metadata => metadata.NetEntity.Valid
                        && metadata.NetEntity != firstNetworkEntity
                        && metadata.NetEntity != secondNetworkEntity)
                    .Select(metadata => new EntityState(
                        metadata.NetEntity,
                        Array.Empty<ComponentChange>(),
                        timing.CurTick))
                    .ToArray();

                var state = new GameState(
                    GameTick.Zero,
                    timing.CurTick,
                    0,
                    entities,
                    Array.Empty<SessionState>(),
                    Array.Empty<NetEntity>());
                var persistentClientEntity = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
                try
                {
                    entityManager.EventBus.RaiseEvent(
                        EventSource.Network,
                        new RoundRestartNetworkEntityCleanupEvent());

                    var reentered = false;
                    probeSystem.OnTerminating = () =>
                    {
                        if (reentered)
                            return;

                        reentered = true;
                        // Production builds permit this re-entry while a full reset is deleting entities. Mirror those
                        // release semantics in the debug integration test so the deletion workset is exercised.
                        timing.EndStateApplication();
                        gameStateManager.PartialStateReset(state, resetAllEntities: true);
                    };

                    Assert.DoesNotThrow(() => gameStateManager.PartialStateReset(
                        state,
                        resetAllEntities: true));
                    Assert.That(reentered, Is.False,
                        "Network cleanup must remove stale entities before the following full-state reset.");
                    Assert.Multiple(() =>
                    {
                        Assert.That(entityManager.EntityExists(firstClientEntity.Value), Is.False);
                        Assert.That(entityManager.EntityExists(secondClientEntity.Value), Is.False);
                        Assert.That(entityManager.EntityExists(persistentClientEntity), Is.True);
                    });
                }
                finally
                {
                    probeSystem.OnTerminating = null;
                    if (entityManager.EntityExists(persistentClientEntity))
                        entityManager.DeleteEntity(persistentClientEntity);
                }
            });
        }
        finally
        {
            await Server.WaitAssertion(() =>
            {
                if (SEntMan.TryGetEntity(firstNetworkEntity, out var first))
                    SEntMan.DeleteEntity(first);
                if (SEntMan.TryGetEntity(secondNetworkEntity, out var second))
                    SEntMan.DeleteEntity(second);
            });
        }
    }

    [Test]
    public async Task RepeatedServerRestartsPreserveClientEntitiesAndResumeReplication()
    {
        const int restartCount = 5;
        const int rootsPerRestart = 64;
        const int childrenPerRoot = 8;
        EntityUid persistentClientEntity = default;

        await Client.WaitAssertion(() =>
            persistentClientEntity = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace));

        try
        {
            for (var restart = 0; restart < restartCount; restart++)
            {
                var networkEntities = new NetEntity[rootsPerRestart * (childrenPerRoot + 1)];
                await Server.WaitAssertion(() =>
                {
                    var pvsOverride = Server.System<ServerPvsOverrideSystem>();
                    var transform = SEntMan.System<SharedTransformSystem>();
                    var index = 0;
                    for (var rootIndex = 0; rootIndex < rootsPerRestart; rootIndex++)
                    {
                        var root = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                        networkEntities[index++] = SEntMan.GetNetEntity(root);
                        for (var childIndex = 0; childIndex < childrenPerRoot; childIndex++)
                        {
                            var child = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                            transform.SetParent(child, root);
                            networkEntities[index++] = SEntMan.GetNetEntity(child);
                        }

                        pvsOverride.AddSessionOverride(root, ServerSession!);
                    }
                });
                await Pair.RunUntilSynced();

                await Client.WaitAssertion(() =>
                {
                    foreach (var networkEntity in networkEntities)
                        Assert.That(CEntMan.TryGetEntity(networkEntity, out _), Is.True);
                });

                await Server.WaitPost(() => Server.System<GameTicker>().RestartRound());
                await Pair.RunUntilSynced();

                await Client.WaitAssertion(() =>
                {
                    Assert.That(CEntMan.EntityExists(persistentClientEntity), Is.True);
                    Assert.That(Client.ResolveDependency<IStateManager>().CurrentState, Is.TypeOf<LobbyState>());
                    foreach (var networkEntity in networkEntities)
                        Assert.That(CEntMan.TryGetEntity(networkEntity, out _), Is.False);
                });
            }
        }
        finally
        {
            await Client.WaitAssertion(() =>
            {
                if (CEntMan.EntityExists(persistentClientEntity))
                    CEntMan.DeleteEntity(persistentClientEntity);
            });
        }
    }
}

[RegisterComponent]
public sealed partial class FullStateResetReentryProbeComponent : Component;

public sealed class FullStateResetReentryProbeSystem : EntitySystem
{
    public Action? OnTerminating;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FullStateResetReentryProbeComponent, EntityTerminatingEvent>(OnProbeTerminatingEvent);
    }

    private void OnProbeTerminatingEvent(
        Entity<FullStateResetReentryProbeComponent> entity,
        ref EntityTerminatingEvent args)
    {
        OnTerminating?.Invoke();
    }
}
