using Latios;
using Latios.Kinemation;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using System;
using static Unity.Entities.SystemAPI;

namespace Dragons
{
    [BurstCompile]
    public partial struct PoleTest : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            state.RequireForUpdate<GameObjectEntity.ExistComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var target in Query<WorldTransform>().WithAll<GameObjectEntity.ExistComponent>())
            {
                if (target.rotation.value.Equals(float4.zero))
                    return;
                foreach (var skeleton in Query<OptimizedSkeletonAspect>())
                {
                    var solver = new SimpleConeConstraintSolver
                    {
                        cosY             = math.cos(math.radians(10f)),
                        sinX             = math.sin(math.radians(10f)),
                        numIterations    = 12,
                        dampedIterations = 2,
                        dampFactor       = 0.7f,
                        //useConstraints   = true
                    };
                    Span<Ewbik.Target> targetSpan = stackalloc Ewbik.Target[1];
                    targetSpan[0]                 = new Ewbik.Target
                    {
                        boneIndex                                    = (short)(skeleton.boneCount - 2),  // I told blender to not add a leaf bone, but...
                        boneLocalPositionOffsetToMatchTargetPosition = math.up(),
                        positionWeight                               = 1f,
                        rotationWeight                               = 0f,
                        rootRelativePosition                         = target.position,
                        rootRelativeRotation                         = target.rotation,
                        targetUserId                                 = 0,
                    };
                    var skeletonCopy = skeleton;
                    //UnityEngine.Debug.Log($"Ewbik with target: {targetSpan[0].rootRelativePosition}, {targetSpan[0].rootRelativeRotation}");
                    Ewbik.Solve(ref skeletonCopy, ref targetSpan, ref solver);
                }
            }
        }

        struct SimpleConeConstraintSolver : Ewbik.IConstraintSolver
        {
            public float cosY;
            public float sinX;
            public int   numIterations;
            public int   dampedIterations;
            public float dampFactor;
            public bool  useConstraints;

            public bool ApplyConstraintsToBone(OptimizedBone bone, in RigidTransform proposedTransformDelta, in Ewbik.BoneSolveState boneSolveState)
            {
                var newRotation = math.normalize(qvvs.InverseTransformRotation(bone.parent.rootTransform, math.mul(proposedTransformDelta.rot, bone.rootRotation)));
                var newUp       = math.mul(newRotation, math.up());
                var newRight    = math.normalize(math.mul(newRotation, math.right()).xz);
                //UnityEngine.Debug.Log($"Iteration: {boneSolveState.iterationsCompletedForSkeleton}, index: {bone.index}, newUp: {newUp}, newRight: {newRight}");
                if (!useConstraints)
                {
                    bone.localRotation = newRotation;
                    return false;
                }
                if (newUp.y < cosY)
                {
                    // ay / sqrt(ay * ay + x * x + z * z) = cosY
                    // ay = cosY * sqrt(ay * ay + x * x + z * z)
                    // ay * ay = cosY * cosY * (ay * ay + x * x + z * z)
                    // ay * ay - cosY * cosY * ay * ay = cosY * cosY * (x * x + z * z)
                    // (1 - cosY * cosY)(ay * ay) = cosY * cosY * (x * x + z * z)
                    // a * a = cosY * cosY * (x * x + z * z) / (y * y - y * y * cosY * cosY)
                    var yScale  = math.sqrt(cosY * cosY * math.lengthsq(newUp.xz) / (newUp.y * newUp.y * (1 - cosY * cosY)));
                    newUp.y    *= yScale;
                    newUp       = math.normalize(newUp);
                }

                if (math.abs(newRight.y) > sinX)
                {
                    newRight.y = math.sign(newRight.y) * sinX;
                    newRight.x = math.sqrt(1 - newRight.y * newRight.y);
                }
                var targetRight   = newRight.x0y();
                var targetForward = math.cross(targetRight, newUp);
                targetRight       = math.cross(newUp, targetForward);
                newRotation       = new quaternion(new float3x3(targetRight, newUp, targetForward));
                if (boneSolveState.iterationsCompletedForSkeleton < dampedIterations)
                {
                    newRotation = math.nlerp(bone.localRotation, newRotation, dampFactor);
                }
                bone.localRotation = newRotation;
                return false;
            }

            public bool IsFixedToParent(OptimizedBone bone)
            {
                return bone.index <= 2;
            }

            public bool NeedsSkeletonIteration(OptimizedSkeletonAspect skeleton, ReadOnlySpan<Ewbik.Target> sortedTargets, int iterationsPerformedSoFar)
            {
                return iterationsPerformedSoFar < numIterations;
            }

            public bool UseTranslationInSolve(OptimizedBone bone, int iterationsSoFarForThisBone)
            {
                return false;
            }
        }
    }
}

