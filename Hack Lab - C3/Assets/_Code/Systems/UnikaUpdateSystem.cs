using Latios;
using Latios.Transforms;
using Latios.Unika;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace C3
{
    public struct UnikaUpdateContext
    {
        public Entity worldBlackboardEntity;
        public Entity sceneBlackboardEntity;
        public double elapsedTime;
        public float  deltaTime;

        public ComponentBroker ecs;
    }

    public partial interface IUnikaUpdate : IUnikaInterface
    {
        public void Update(ref UnikaUpdateContext context, Script thisScript);
    }

    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct UnikaUpdateSystem : ISystem, ISystemNewScene
    {
        LatiosWorldUnmanaged latiosWorld;
        UnikaUpdateContext   updateContext;

        EntityQuery m_query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld   = state.GetLatiosWorldUnmanaged();
            updateContext = new UnikaUpdateContext
            {
                worldBlackboardEntity = latiosWorld.worldBlackboardEntity,
                ecs                   = new ComponentBrokerBuilder(Allocator.Temp).With<UnikaScripts>().WithTransformAspect().Build(ref state, Allocator.Persistent)
            };
            m_query = state.Fluent().With<UnikaScripts>().Build();
            latiosWorld.worldBlackboardEntity.AddBuffer<UnikaScripts>();
        }

        public void OnNewScene(ref SystemState state) => updateContext.sceneBlackboardEntity = latiosWorld.sceneBlackboardEntity;

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            updateContext.ecs.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            updateContext.deltaTime   = SystemAPI.Time.DeltaTime;
            updateContext.elapsedTime = SystemAPI.Time.ElapsedTime;
            updateContext.ecs.Update(ref state);
            var job = new Job { updateContext = updateContext };
            state.Dependency                  = job.ScheduleParallelByRef(m_query, state.Dependency);
        }

        [BurstCompile]
        struct Job : IJobChunk
        {
            public UnikaUpdateContext updateContext;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var scriptsBuffers = chunk.GetBufferAccessor<UnikaScripts>(ref updateContext.ecs);
                var entities       = chunk.GetNativeArray(updateContext.ecs.entityTypeHandle);
                for (int i = 0; i < scriptsBuffers.Length; i++)
                {
                    updateContext.ecs.SetupEntity(in chunk, i);
                    var allScripts = scriptsBuffers[i].AllScripts(entities[i]);
                    foreach (var script in allScripts.Of<IUnikaUpdate.Interface>())
                    {
                        script.Update(ref updateContext, script);
                    }
                }
            }
        }
    }
}

