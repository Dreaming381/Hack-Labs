using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace A1
{
    [BurstCompile]
    public partial struct BuildStaticEnvironmentCollisionLayerSystem : ISystem, ISystemNewScene, ISystemShouldUpdate
    {
        LatiosWorldUnmanaged latiosWorld;

        BuildCollisionLayerTypeHandles m_handles;
        EntityQuery                    m_query;
        bool                           m_needsUpdate;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld   = state.GetLatiosWorldUnmanaged();
            m_handles     = new BuildCollisionLayerTypeHandles(ref state);
            m_query       = state.Fluent().With<StaticEnvironmentTag>(true).PatchQueryForBuildingCollisionLayer().Build();
            m_needsUpdate = false;
        }

        public void OnNewScene(ref SystemState state)
        {
            latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld<StaticEnvironmentCollisionLayer>(default);
            m_needsUpdate = true;
        }

        public bool ShouldUpdateSystem(ref SystemState state) => m_needsUpdate;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_needsUpdate = false;
            m_handles.Update(ref state);
            state.Dependency =
                Physics.BuildCollisionLayer(m_query, m_handles).ScheduleParallel(out var layer, Allocator.Persistent, state.Dependency);
            latiosWorld.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(new StaticEnvironmentCollisionLayer { layer = layer });
        }
    }
}

