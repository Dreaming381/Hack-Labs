using Latios;
using Latios.Authoring;
using Latios.Transforms;
using Latios.Unika;
using Latios.Unika.Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace C3
{
    namespace Authoring
    {
        public partial class TempQueryScript : UnikaScriptAuthoring<Scripts.TempQueryScript>
        {
            public override void Bake(IBaker baker, ref AuthoredScriptAssignment toAssign, Entity smartPostProcessTarget)
            {
                var script = new Scripts.TempQueryScript
                {
                };
                toAssign.Assign(ref script);
                toAssign.transformUsageFlags = TransformUsageFlags.Renderable;
                toAssign.userFlagA           = false;
                toAssign.userFlagB           = false;
                toAssign.userByte            = 0;
            }

            public override bool IsValid()
            {
                return true;
            }
        }
    }

    namespace Scripts
    {
        public partial struct TempQueryScript : IUnikaScript, IUnikaUpdate
        {
            public void Update(ref UnikaUpdateContext context, Script thisScript)
            {
                var query      = new TempQuery(context.archetypes.AsArray(), context.ecs.entityStorageInfoLookup, new TypePack<WorldTransform>());
                int queryCount = 0;
                int queryIndex = -1;
                foreach (var entityInQuery in query.entities)
                {
                    if (entityInQuery == thisScript.entity)
                    {
                        queryIndex = queryCount;
                    }
                    queryCount++;
                }

                UnityEngine.Debug.Log($"TempQuery found {queryCount} entities with transforms and this entity was at index {queryIndex}");
            }
        }

        [BurstCompile]
        partial struct TestWrapper
        {
            partial struct Col : ICollectionComponent
            {
                public JobHandle TryDispose(JobHandle inputDeps)
                {
                    return inputDeps;
                }

                void WhatHappened() {
                }
            }
        }
    }
}

