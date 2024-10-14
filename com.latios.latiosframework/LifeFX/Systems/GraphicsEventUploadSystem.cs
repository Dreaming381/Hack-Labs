using Latios.Kinemation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.LifeFX.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct GraphicsEventUploadSystem : ISystem, ICullingComputeDispatchSystem<GraphicsEventUploadSystem.CollectState, GraphicsEventUploadSystem.WriteState>
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }

        public CollectState Collect(ref SystemState state)
        {
            throw new System.NotImplementedException();
        }

        public WriteState Write(ref SystemState state, ref CollectState collected)
        {
            throw new System.NotImplementedException();
        }

        public void Dispatch(ref SystemState state, ref WriteState written)
        {
            throw new System.NotImplementedException();
        }

        public struct CollectState
        {
            internal GraphicsBufferBroker broker;
        }

        public struct WriteState
        {
            internal GraphicsBufferBroker broker;
        }
    }
}

