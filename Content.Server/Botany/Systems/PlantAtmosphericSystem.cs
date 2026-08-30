using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;

namespace Content.Server.Botany.Systems;

public sealed partial class PlantAtmosphericSystem : SharedPlantAtmosphericSystem
{
    [Dependency] private EntityQuery<PlantHolderComponent> _holderQuery = default!;

    [SubscribeLocalEvent]
    private void OnPlantGrow(Entity<PlantAtmosphericComponent> ent, ref PlantGrowEvent args)
    {
        if (!_holderQuery.TryComp(ent.Owner, out var holder))
            return;

        if (!holder.ImproperHeat && !holder.ImproperPressure)
            return;

        holder.ImproperHeat = false;
        holder.ImproperPressure = false;
        DirtyFields(ent.Owner, holder, null, nameof(holder.ImproperHeat), nameof(holder.ImproperPressure));
    }
}
