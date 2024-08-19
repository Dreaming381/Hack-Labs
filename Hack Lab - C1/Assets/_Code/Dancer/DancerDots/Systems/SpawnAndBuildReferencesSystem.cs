using System.Collections.Generic;
using Latios;
using Latios.Kinemation;
using Latios.Transforms;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Jobs;

using Random = UnityEngine.Random;
using static Unity.Entities.SystemAPI;

namespace Dragons
{
    public partial class SpawnAndBuildReferencesSystem : SubSystem
    {
        struct ColorInitializedTag : IComponentData { }

        EntityQuery m_spawnerQuery;
        EntityQuery m_dancerColorQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            CheckedStateRef.InitSystemRng("SpawnAndBuildReferencesSystem");
            m_spawnerQuery     = Fluent.With<DancerReferenceGroupSourcePrefab, SpawnerDots>(true).Build();
            m_dancerColorQuery = Fluent.With<URPMaterialPropertyBaseColor>().Without<ColorInitializedTag>().Build();
        }

        protected override void OnUpdate()
        {
            var icb                = new InstantiateCommandBuffer<WorldTransform, DancerDots>(Allocator.TempJob);
            var icbp               = icb.AsParallelWriter();
            var dcb                = new DestroyCommandBuffer(Allocator.TempJob);
            var renderersInPrefabs = new NativeList<Entity>(Allocator.TempJob);

            var entities = m_spawnerQuery.ToEntityArray(Allocator.Temp);

            CompleteDependency();
            foreach (var entity in entities)
            {
                var        source   = SystemAPI.GetComponent<DancerReferenceGroupSourcePrefab>(entity);
                var        spawner  = SystemAPI.GetComponent<SpawnerDots>(entity);
                GameObject goPrefab = source.dancerGoPrefab;

                source.bonesPerReference = SystemAPI.GetComponent<OptimizedSkeletonHierarchyBlobReference>(spawner.dancerPrefab).blob.Value.parentIndices.Length;
                SystemAPI.SetComponent(entity, source);

                var transformArray = new DancerReferenceGroupTransforms
                {
                    transforms = new TransformAccessArray(source.bonesPerReference * spawner.referencesToSpawn)
                };

                for (int i = 0; i < spawner.referencesToSpawn; i++)
                {
                    var newGo  = GameObject.Instantiate(goPrefab);
                    var newSmr = newGo.GetComponentInChildren<SkinnedMeshRenderer>();
                    AddTransformsInSkeletonOrder(transformArray.transforms, newGo, EntityManager.GetComponentData<SkeletonBindingPathsBlobReference>(spawner.dancerPrefab));
                    newSmr.enabled = false;
                    InitializeReference(newGo);
                }
                latiosWorldUnmanaged.AddOrSetCollectionComponentAndDisposeOld(entity, transformArray);

                var prefab = EntityManager.Instantiate(spawner.dancerPrefab);
                foreach (var child in EntityManager.GetBuffer<LinkedEntityGroup>(prefab))
                {
                    if (EntityManager.HasComponent<MaterialMeshInfo>(child.Value))
                    {
                        renderersInPrefabs.Add(child.Value);
                    }
                }
                EntityManager.AddSharedComponent(prefab, new DancerReferenceGroupMember { dancerReferenceEntity = entity });
                var cache                                                                                       = EntityManager.AddBuffer<QuaternionCacheElement>(prefab);
                cache.ResizeUninitialized(source.bonesPerReference);
                for (int i = 0; i < source.bonesPerReference; i++)
                    cache[i] = default;
                EntityManager.AddComponent<DancerDots>(prefab);

                new InstantiateJob
                {
                    icb           = icbp,
                    patchedPrefab = prefab,
                    rng           = CheckedStateRef.GetJobRng(),
                    spawner       = spawner
                }.ScheduleParallel(spawner.columns, 1, default).Complete();
                dcb.Add(prefab);
            }

            EntityManager.AddComponent<URPMaterialPropertyBaseColor>(renderersInPrefabs.AsArray());
            renderersInPrefabs.Dispose();
            icb.Playback(EntityManager);
            icb.Dispose();
            dcb.Playback(EntityManager);
            dcb.Dispose();

            new InitializeColorJob { rng = CheckedStateRef.GetJobRng() }.ScheduleParallel();
            CompleteDependency();
            EntityManager.AddComponent<ColorInitializedTag>(m_dancerColorQuery);
            EntityManager.RemoveComponent<SpawnerDots>(m_spawnerQuery);
        }

        List<Transform> m_transformsCache = new List<Transform>();

        unsafe void AddTransformsInSkeletonOrder(TransformAccessArray transformArray, GameObject go, SkeletonBindingPathsBlobReference blobRef)
        {
            ref var            paths  = ref blobRef.blob.Value.pathsInReversedNotation;
            FixedString64Bytes goName = default;

            // Assume the roots match even though their names are different.
            transformArray.Add(go.transform);
            go.GetComponentsInChildren(m_transformsCache);

            for (int i = 1; i < paths.Length; i++)
            {
                bool found = false;
                foreach (Transform t in m_transformsCache)
                {
                    goName = t.gameObject.name;
                    if (UnsafeUtility.MemCmp(goName.GetUnsafePtr(), paths[i].GetUnsafePtr(), math.min(goName.Length, 12)) == 0)
                    {
                        transformArray.Add(t);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    FixedString4096Bytes missingPath = default;
                    missingPath.Append((byte*)paths[i].GetUnsafePtr(), paths[i].Length);
                    UnityEngine.Debug.LogWarning($"Failed to find mapping for GO dancer reference. Name: {missingPath}");
                }
            }
        }

        void InitializeReference(GameObject go)
        {
            var dancer = go.GetComponent<Puppet.Dancer>();

            dancer.footDistance *= Random.Range(0.8f, 2.0f);
            //dancer.stepFrequency *= Random.Range(0.4f, 1.6f);
            dancer.stepHeight *= Random.Range(0.75f, 1.25f);
            dancer.stepAngle  *= Random.Range(0.75f, 1.25f);

            dancer.hipHeight        *= Random.Range(0.75f, 1.25f);
            dancer.hipPositionNoise *= Random.Range(0.75f, 1.25f);
            dancer.hipRotationNoise *= Random.Range(0.75f, 1.25f);

            dancer.spineBend           = Random.Range(4.0f, -16.0f);
            dancer.spineRotationNoise *= Random.Range(0.75f, 1.25f);

            dancer.handPositionNoise *= Random.Range(0.5f, 2.0f);
            dancer.handPosition      += Random.insideUnitSphere * 0.25f;

            dancer.headMove *= Random.Range(0.2f, 2.8f);
            //dancer.noiseFrequency *= Random.Range(0.4f, 1.8f);
            dancer.randomSeed = (uint)Random.Range(0, 0xffffff);
        }

        [BurstCompile]
        struct InstantiateJob : IJobFor
        {
            public InstantiateCommandBuffer<WorldTransform, DancerDots>.ParallelWriter icb;
            public SpawnerDots                                                         spawner;
            public Entity                                                              patchedPrefab;
            public SystemRng                                                           rng;

            public void Execute(int c)
            {
                rng.BeginChunk(c);
                var x = spawner.interval * (c - spawner.columns * 0.5f + 0.5f);
                for (int r = 0; r < spawner.rows; r++)
                {
                    var y = spawner.interval * (r - spawner.rows * 0.5f + 0.5f);

                    var trans     = new float3(x, 0f, y);
                    var rot       = quaternion.AxisAngle(math.up(), rng.NextFloat(0f, 2f * math.PI));
                    var transform = new WorldTransform { worldTransform = new TransformQvvs(trans, rot) };
                    var dancer    = new DancerDots
                    {
                        referenceDancerIndexA = rng.NextInt(0, spawner.referencesToSpawn),
                        referenceDancerIndexB = rng.NextInt(0, spawner.referencesToSpawn),
                        weightA               = rng.NextFloat(0.0f, 1f)
                    };
                    icb.Add(patchedPrefab, transform, dancer, c);
                }
            }
        }

        [WithNone(typeof(ColorInitializedTag))]
        [BurstCompile]
        partial struct InitializeColorJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public SystemRng rng;

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                rng.BeginChunk(unfilteredChunkIndex);
                return true;
            }

            public void Execute(ref URPMaterialPropertyBaseColor color)
            {
                var hsv     = rng.NextFloat3(new float3(0f, 0.6f, 0.8f), new float3(1f, 0.8f, 1f));
                var rgb     = Color.HSVToRGB(hsv.x, hsv.y, hsv.z);
                color.Value = new float4(rgb.r, rgb.g, rgb.b, 1f);
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }
    }
}

