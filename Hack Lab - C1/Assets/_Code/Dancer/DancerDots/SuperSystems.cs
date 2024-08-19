using Latios;
using Latios.Kinemation.Systems;
using Latios.Systems;
using Latios.Transforms;
using Latios.Transforms.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Dragons
{
    [UpdateInGroup(typeof(LatiosWorldSyncGroup))]
    [UpdateBefore(typeof(KinemationFrameSyncPointSuperSystem))]
    public partial class SyncSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<SpawnAndBuildReferencesSystem>();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSuperSystem))]
    public partial class UpdateSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddManagedSystem<SampleDancersOptimizedSystem>();
        }
    }
}

