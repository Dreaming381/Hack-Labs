using System.Collections.Generic;
using Latios.Kinemation;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.LifeFX.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct GraphicsEventUploadSystem : ISystem, ICullingComputeDispatchSystem<GraphicsEventUploadSystem.CollectState, GraphicsEventUploadSystem.WriteState>
    {
        LatiosWorldUnmanaged latiosWorld;

        CullingComputeDispatchData<CollectState, WriteState> m_data;
        EntityQuery                                          m_destinationsQuery;
        AllocatorHelper<RewindableAllocator>                 m_allocator;

        public void OnCreate(ref SystemState state)
        {
            latiosWorld         = state.GetLatiosWorldUnmanaged();
            m_data              = new CullingComputeDispatchData<CollectState, WriteState>(latiosWorld);
            m_destinationsQuery = state.Fluent().With<GraphicsEventTunnelDestination>(true).Build();
            m_allocator         = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
            m_allocator.Allocator.Initialize(16 * 1024);

            GraphicsEventTypeRegistry.Init();
            latiosWorld.worldBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new GraphicsEventPostal(m_allocator.Allocator.Handle));
            var broker = latiosWorld.worldBlackboardEntity.GetComponentData<GraphicsBufferBroker>();
            foreach (var meta in GraphicsEventTypeRegistry.s_eventMetadataList.Data)
            {
                broker.InitializeUploadPool(meta.brokerId, (uint)meta.size, UnityEngine.GraphicsBuffer.Target.Structured);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            m_allocator.Allocator.Rewind();
            m_allocator.Allocator.Dispose();
            m_allocator.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_data.DoUpdate(ref state, ref this);
        }

        public CollectState Collect(ref SystemState state)
        {
            if (m_destinationsQuery.IsEmptyIgnoreFilter)
            {
                m_allocator.Allocator.Rewind();
                return default;
            }

            var eventTypeCount          = GraphicsEventTypeRegistry.s_eventMetadataList.Data.Length;
            var allocator               = m_allocator.Allocator.Handle;
            var destinations            = new NativeList<GraphicsEventTunnelDestination>(allocator);
            var tunnels                 = new NativeList<UnityObjectRef<GraphicsEventTunnelBase> >(allocator);
            var tunnelRangesByTypeIndex = CollectionHelper.CreateNativeArray<int2>(eventTypeCount, allocator, NativeArrayOptions.UninitializedMemory);

            var job = new CollectDestinationsJob
            {
                chunks                  = m_destinationsQuery.ToArchetypeChunkListAsync(allocator, out var jh).AsDeferredJobArray(),
                destinationHandle       = GetBufferTypeHandle<GraphicsEventTunnelDestination>(true),
                destinations            = destinations,
                tunnels                 = tunnels,
                tunnelRangesByTypeIndex = tunnelRangesByTypeIndex
            };
            state.Dependency = job.Schedule(JobHandle.CombineDependencies(state.Dependency, jh));

            var postal                         = latiosWorld.GetCollectionComponent<GraphicsEventPostal>(latiosWorld.worldBlackboardEntity, false);
            var eventCountByTypeIndex          = CollectionHelper.CreateNativeArray<int>(eventTypeCount, allocator, NativeArrayOptions.UninitializedMemory);
            var eventRangesByTunnelByTypeIndex = CollectionHelper.CreateNativeArray<UnsafeList<int2> >(eventTypeCount, allocator, NativeArrayOptions.UninitializedMemory);
            state.Dependency                   = new GroupAndCountEventsJob
            {
                tunnels                        = tunnels.AsDeferredJobArray(),
                tunnelRangesByTypeIndex        = tunnelRangesByTypeIndex,
                postal                         = postal,
                eventCountByTypeIndex          = eventCountByTypeIndex,
                eventRangesByTunnelByTypeIndex = eventRangesByTunnelByTypeIndex,
                allocator                      = allocator,
            }.ScheduleParallel(eventTypeCount, 1, state.Dependency);

            return new CollectState
            {
                broker                         = latiosWorld.worldBlackboardEntity.GetComponentData<GraphicsBufferBroker>(),
                tunnels                        = tunnels,
                tunnelRangesByTypeIndex        = tunnelRangesByTypeIndex,
                postal                         = postal,
                eventCountByTypeIndex          = eventCountByTypeIndex,
                eventRangesByTunnelByTypeIndex = eventRangesByTunnelByTypeIndex
            };
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
            internal GraphicsBufferBroker                                 broker;
            internal NativeList<UnityObjectRef<GraphicsEventTunnelBase> > tunnels;
            internal NativeArray<int2>                                    tunnelRangesByTypeIndex;
            internal GraphicsEventPostal                                  postal;
            internal NativeArray<int>                                     eventCountByTypeIndex;
            internal NativeArray<UnsafeList<int2> >                       eventRangesByTunnelByTypeIndex;
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

        [BurstCompile]
        struct GroupAndCountEventsJob : IJobFor
        {
            [ReadOnly] public NativeArray<UnityObjectRef<GraphicsEventTunnelBase> > tunnels;
            [ReadOnly] public NativeArray<int2>                                     tunnelRangesByTypeIndex;
            [ReadOnly] public GraphicsEventPostal                                   postal;

            public NativeArray<int>                 eventCountByTypeIndex;
            public NativeArray<UnsafeList<int2> >   eventRangesByTunnelByTypeIndex;
            public AllocatorManager.AllocatorHandle allocator;

            public void Execute(int typeIndex)
            {
                var tunnelsRange = tunnelRangesByTypeIndex[typeIndex];
                if (tunnelsRange.y == 0)
                {
                    eventCountByTypeIndex[typeIndex]          = 0;
                    eventRangesByTunnelByTypeIndex[typeIndex] = default;
                    return;
                }

                var tunnelsOfType = tunnels.GetSubArray(tunnelsRange.x, tunnelsRange.y);
                var eventRanges   = new UnsafeList<int2>(tunnelsOfType.Length, allocator);
                eventRanges.Resize(tunnelsOfType.Length, NativeArrayOptions.ClearMemory);
                var tunnelToIndexMap = new NativeHashMap<UnityObjectRef<GraphicsEventTunnelBase>, int>(eventRanges.Length, Allocator.Temp);
                int tunnelIndex      = 0;
                foreach (var tunnel in tunnelsOfType)
                {
                    tunnelToIndexMap.Add(tunnel, tunnelIndex);
                    tunnelIndex++;
                }

                var blocklist  = postal.blocklistPairArray[typeIndex].tunnelTargets;
                var enumerator = blocklist.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var target = enumerator.GetCurrent<UnityObjectRef<GraphicsEventTunnelBase> >();
                    if (tunnelToIndexMap.TryGetValue(target, out var index))
                    {
                        eventRanges.ElementAt(index).y++;
                    }
                }

                int total = 0;
                for (int i = 0; i < eventRanges.Length; i++)
                {
                    ref var range  = ref eventRanges.ElementAt(i);
                    range.x        = total;
                    total         += range.y;
                    range.y        = 0;
                }

                eventCountByTypeIndex[typeIndex]          = total;
                eventRangesByTunnelByTypeIndex[typeIndex] = eventRanges;
            }
        }

        [BurstCompile]
        struct WriteEventsJob : IJobFor
        {
            [ReadOnly] public NativeArray<UnityObjectRef<GraphicsEventTunnelBase> > tunnels;
            [ReadOnly] public NativeArray<int2>                                     tunnelRangesByTypeIndex;
            [ReadOnly] public GraphicsEventPostal                                   postal;

            // Technically could be ReadOnly, but that might confuse Burst's alias checker. Something to maybe experiment with later.
            public NativeArray<UnsafeList<int2> > eventRangesByTunnelByTypeIndex;
            public NativeArray<UnsafeList<byte> > buffers;

            public unsafe void Execute(int typeIndex)
            {
                var tunnelsRange = tunnelRangesByTypeIndex[typeIndex];
                if (tunnelsRange.y == 0)
                    return;

                var tunnelsOfType    = tunnels.GetSubArray(tunnelsRange.x, tunnelsRange.y);
                var eventRanges      = eventRangesByTunnelByTypeIndex[typeIndex];
                var tunnelToIndexMap = new NativeHashMap<UnityObjectRef<GraphicsEventTunnelBase>, int>(eventRanges.Length, Allocator.Temp);
                int tunnelIndex      = 0;
                foreach (var tunnel in tunnelsOfType)
                {
                    tunnelToIndexMap.Add(tunnel, tunnelIndex);
                    tunnelIndex++;
                }

                var blocklistPair    = postal.blocklistPairArray[typeIndex];
                var targetEnumerator = blocklistPair.tunnelTargets.GetEnumerator();
                var eventEnumerator  = blocklistPair.events.GetEnumerator();
                var eventSize        = blocklistPair.events.elementSize;
                var buffer           = buffers[typeIndex];
                while (targetEnumerator.MoveNext())
                {
                    eventEnumerator.MoveNext();
                    var target = targetEnumerator.GetCurrent<UnityObjectRef<GraphicsEventTunnelBase> >();
                    if (tunnelToIndexMap.TryGetValue(target, out var index))
                    {
                        ref var range = ref eventRanges.ElementAt(index);
                        var     src   = eventEnumerator.GetCurrentPtr();
                        var     dst   = UnsafeUtility.AddressOf(ref buffer.ElementAt((range.x + range.y) * eventSize));
                        UnsafeUtility.MemCpy(dst, src, eventSize);
                        range.y++;
                    }
                }
            }
        }
    }
}

