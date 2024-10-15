using System.Collections.Generic;
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

        [BurstCompile]
        struct CollectDestinationsJob : IJob
        {
            [ReadOnly] public NativeArray<ArchetypeChunk>                      chunks;
            [ReadOnly] public BufferTypeHandle<GraphicsEventTunnelDestination> destinationHandle;

            public NativeList<GraphicsEventTunnelDestination>           destinations;
            public NativeList<UnityObjectRef<GraphicsEventTunnelBase> > tunnels;
            public NativeArray<int2>                                    tunnelRangesByTypeIndex;

            public void Execute()
            {
                var uniqueDestinations = new NativeHashSet<GraphicsEventTunnelDestination>();
                foreach (var chunk in chunks)
                {
                    var destinationAccessor = chunk.GetBufferAccessor(ref destinationHandle);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var buffer = destinationAccessor[i];
                        foreach (var d in buffer)
                            uniqueDestinations.Add(d);
                    }
                }

                {
                    destinations.ResizeUninitialized(uniqueDestinations.Count);
                    int i = 0;
                    foreach (var d in uniqueDestinations)
                    {
                        destinations[i] = d;
                        i++;
                    }
                }

                destinations.Sort(new Comparer());
                tunnels.Capacity = destinations.Length;
                tunnelRangesByTypeIndex.AsSpan().Clear();
                var previousTunnel = default(UnityObjectRef<GraphicsEventTunnelBase>);
                foreach (var d in destinations)
                {
                    if (d.tunnel == previousTunnel)
                        continue;
                    previousTunnel    = d.tunnel;
                    var startAndCount = tunnelRangesByTypeIndex[d.eventTypeIndex];
                    if (startAndCount.y == 0)
                        tunnelRangesByTypeIndex[d.eventTypeIndex] = new int2(tunnels.Length, 1);
                    else
                        tunnelRangesByTypeIndex[d.eventTypeIndex] = new int2(startAndCount.x, startAndCount.y + 1);
                    tunnels.Add(d.tunnel);
                }
            }

            struct Comparer : IComparer<GraphicsEventTunnelDestination>
            {
                public int Compare(GraphicsEventTunnelDestination x, GraphicsEventTunnelDestination y)
                {
                    var result = x.eventTypeIndex.CompareTo(y.eventTypeIndex);
                    if (result == 0)
                        result = x.tunnel.GetHashCode().CompareTo(y.tunnel.GetHashCode());
                    if (result == 0)
                        result = x.requestor.GetHashCode().CompareTo(y.requestor.GetHashCode());
                    return result;
                }
            }
        }
    }
}

