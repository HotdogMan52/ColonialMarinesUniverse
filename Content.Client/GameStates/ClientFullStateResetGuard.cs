using System.Reflection;
using Robust.Client.GameStates;
using Robust.Shared.GameStates;

namespace Content.Client.GameStates;

/// <summary>
/// Prepares stale transform hierarchies before RobustToolbox applies a full state.
/// </summary>
/// <remarks>
/// The engine's full-state reset detaches transform children while directly enumerating the same child collection.
/// Release builds catch the resulting collection-modified exception at the game-loop boundary, leaving the client on
/// an incomplete state. Content has no public pre-apply state event, so this guard reads the already-buffered full
/// state during <see cref="ModUpdateLevel.PreEngine"/> and empties only the stale parents that the reset will remove.
/// </remarks>
public sealed class ClientFullStateResetGuard
{
    [Dependency] private IClientGameStateManager _gameStates = default!;
    [Dependency] private IEntityManager _entities = default!;

    private FieldInfo _processorField = default!;
    private PropertyInfo _lastFullStateProperty = default!;
    private GameState? _preparedState;

    public void Initialize()
    {
        _processorField = _gameStates.GetType().GetField("_processor", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Client game-state processor field was not found.");
        _lastFullStateProperty = _processorField.FieldType.GetProperty("LastFullState", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException("Pending full-state property was not found.");
    }

    public void PreparePendingFullState()
    {
        var processor = _processorField.GetValue(_gameStates);
        if (processor == null || _lastFullStateProperty.GetValue(processor) is not GameState state)
            return;

        if (ReferenceEquals(_preparedState, state))
            return;

        PrepareState(state);
        _preparedState = state;
    }

    internal void PrepareState(GameState state)
    {
        var stateEntities = new HashSet<NetEntity>();
        foreach (var entityState in state.EntityStates.Span)
        {
            stateEntities.Add(entityState.NetEntity);
        }

        var staleEntities = new List<EntityUid>();
        var query = _entities.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var metadata, out _))
        {
            if (!metadata.NetEntity.IsClientSide() && !stateEntities.Contains(metadata.NetEntity))
                staleEntities.Add(uid);
        }

        foreach (var uid in staleEntities)
        {
            PrepareStaleEntity(uid);
        }
    }

    internal void PrepareStaleEntity(EntityUid uid)
    {
        if (!_entities.TryGetComponent(uid, out TransformComponent? transform))
            return;

        var children = new List<EntityUid>();
        var enumerator = transform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            children.Add(child);
        }

        var transformSystem = _entities.System<SharedTransformSystem>();
        foreach (var child in children)
        {
            transformSystem.DetachEntity(child);

            // PartialStateReset's default behavior is to delete client-only children of stale network entities.
            if (_entities.IsClientSide(child))
                _entities.DeleteEntity(child);
        }
    }
}
