using Latios;
using Latios.Kinemation;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

using static Unity.Entities.SystemAPI;

namespace Dragons
{
    public partial class SampleDancersOptimizedSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            foreach ((var group, var entity) in Query<DancerReferenceGroupSourcePrefab>().WithEntityAccess())
            {
                DoSampling(entity, group);
            }
        }

        void DoSampling(Entity entity, DancerReferenceGroupSourcePrefab group)
        {
            var memberScd           = new DancerReferenceGroupMember { dancerReferenceEntity = entity };
            int boneCount                                                                    = group.bonesPerReference;
            var transformCollection                                                          = EntityManager.GetCollectionComponent<DancerReferenceGroupTransforms>(entity, true);
            int transformCount                                                               = transformCollection.transforms.length;

            var srcTransforms = CollectionHelper.CreateNativeArray<RigidTransform>(transformCount, WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);

            Dependency = new FetchTransformsJob { srcTransforms = srcTransforms }.ScheduleReadOnly(transformCollection.transforms, 1, Dependency);

            float dt = SystemAPI.Time.DeltaTime;

            if (HybridSkinningToggle.EnableBlending)
            {
                var query = QueryBuilder().WithAspect<OptimizedSkeletonAspect>().WithAllRW<QuaternionCacheElement>().WithAll<DancerDots, DancerReferenceGroupMember>().Build();
                query.SetSharedComponentFilter(memberScd);
                new BlendedBonesJob { srcTransforms = srcTransforms, dt = dt }.ScheduleParallel(query);
            }
            else
            {
                var query = QueryBuilder().WithAspect<OptimizedSkeletonAspect>().WithAll<DancerDots, DancerReferenceGroupMember>().Build();
                query.SetSharedComponentFilter(memberScd);
                new CopyBonesJob { srcTransforms = srcTransforms, dt = dt }.ScheduleParallel(query);
            }
        }

        [BurstCompile]
        struct FetchTransformsJob : IJobParallelForTransform
        {
            public NativeArray<RigidTransform> srcTransforms;

            public void Execute(int index, TransformAccess transform)
            {
                srcTransforms[index] = new RigidTransform(transform.localRotation, transform.localPosition);
            }
        }

        [BurstCompile]
        partial struct BlendedBonesJob : IJobEntity
        {
            [ReadOnly] public NativeArray<RigidTransform> srcTransforms;
            public float                                  dt;

            public void Execute(OptimizedSkeletonAspect skeleton, ref DynamicBuffer<QuaternionCacheElement> cacheBuffer, in DancerDots dd)
            {
                var bones     = skeleton.rawLocalTransformsRW;
                var boneCount = bones.Length;

                for (int i = 0; i < boneCount; i++)
                {
                    int ia = dd.referenceDancerIndexA * boneCount + i;
                    int ib = dd.referenceDancerIndexB * boneCount + i;
                    var ta = srcTransforms[ia].pos;
                    var tb = srcTransforms[ib].pos;
                    var ra = srcTransforms[ia].rot;
                    var rb = srcTransforms[ib].rot;

                    var t = math.lerp(tb, ta, dd.weightA);

                    var cache = cacheBuffer[i];
                    var r     = cache.lastRotation;

                    if (cache.warmup < 1)
                    {
                        r = math.nlerp(rb, ra, dd.weightA);
                        cache.warmup++;
                    }
                    else
                    {
                        var diffA = math.mul(ra, math.inverse(cache.lastQuaternionA));
                        var diffB = math.mul(rb, math.inverse(cache.lastQuaternionB));

                        float angleA       = math.acos(math.forward(diffA).z);
                        float angleB       = math.acos(math.forward(diffB).z);
                        cache.maxRadsA     = math.max(cache.maxRadsA, angleA / dt);
                        cache.maxRadsB     = math.max(cache.maxRadsB, angleB / dt);
                        float allowedAngle = math.min(math.PI / 2, math.max(cache.maxRadsA, cache.maxRadsB)) * dt;
                        var   target       = math.slerp(rb, ra, dd.weightA);
                        var   targetDelta  = math.mul(target, math.inverse(r));
                        float targetAngle  = math.acos(math.forward(targetDelta).z);
                        r                  = math.slerp(r, target, math.saturate(allowedAngle / targetAngle));
                    }
                    cache.lastQuaternionA = ra;
                    cache.lastQuaternionB = rb;
                    cache.lastRotation    = r;

                    cacheBuffer[i] = cache;

                    bones[i] = new TransformQvvs(t, r);
                }
                SetBoneWeightsToOne(bones);
                skeleton.EndSamplingAndSync();
            }
        }

        [BurstCompile]
        partial struct CopyBonesJob : IJobEntity
        {
            [ReadOnly] public NativeArray<RigidTransform> srcTransforms;
            public float                                  dt;

            public void Execute(OptimizedSkeletonAspect skeleton, in DancerDots dd)
            {
                var bones     = skeleton.rawLocalTransformsRW;
                var boneCount = bones.Length;

                for (int i = 0; i < boneCount; i++)
                {
                    int ia   = dd.referenceDancerIndexA * boneCount + i;
                    var t    = srcTransforms[ia];
                    bones[i] = new TransformQvvs(t);
                }
                SetBoneWeightsToOne(bones);
                skeleton.EndSamplingAndSync();
            }
        }

        static void SetBoneWeightsToOne(NativeArray<TransformQvvs> bones)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                var bone        = bones[i];
                bone.worldIndex = math.asint(1f);
                bones[i]        = bone;
            }
        }
    }
}

