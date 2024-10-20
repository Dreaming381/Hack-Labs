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
                        maxAngleToParent = math.radians(20f),
                        numIterations    = 1,
                        dampedIterations = 2,
                        dampMaxAngle     = math.PI / 100f,
                        useConstraints   = true
                    };
                    Span<Ewbik.Target> targetSpan = stackalloc Ewbik.Target[1];
                    targetSpan[0]                 = new Ewbik.Target
                    {
                        boneIndex                                    = (short)(skeleton.boneCount - 2),  // I told blender to not add a leaf bone, but...
                        boneLocalPositionOffsetToMatchTargetPosition = math.up(),
                        positionWeight                               = 1f,
                        rotationWeight                               = 5f,
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
            public float maxAngleToParent;
            public int   numIterations;
            public int   dampedIterations;
            public float dampMaxAngle;
            public bool  useConstraints;

            public bool ApplyConstraintsToBone(OptimizedBone bone, in RigidTransform proposedTransformDelta, in Ewbik.BoneSolveState boneSolveState)
            {
                var newRotation = proposedTransformDelta.rot;
                var angle       = math.angle(quaternion.identity, newRotation);
                // If the bone didn't move, then we don't need to do anything further.
                if (angle > math.EPSILON)
                {
                    // This limits how much we want to move the bone by each iteration, to prevent "popping".
                    if (angle > dampMaxAngle)
                        newRotation = math.slerp(quaternion.identity, newRotation, dampMaxAngle / angle);

                    // This is just a total angle difference to parent constraint. You'd replace this with your own constraint algorithm.
                    // Remember newRotation is a delta that needs to be applied. That's important for the mean-squared-distance calculation.
                    if (useConstraints)
                    {
                        var newLocalRotation = math.InverseRotateFast(bone.parent.rootRotation, math.mul(newRotation, bone.rootRotation));
                        var parentAngle      = math.angle(quaternion.identity, newLocalRotation);
                        if (parentAngle > maxAngleToParent)
                        {
                            var newConstrainedRotation = math.slerp(quaternion.identity, newLocalRotation, maxAngleToParent / parentAngle);
                            newRotation                = math.InverseRotateFast(bone.localRotation, newConstrainedRotation);
                        }
                    }

                    // Make sure that after all out damping and constraints, that we actually got closer to the solution.
                    // If not, we don't apply the transform.
                    var oldMsd = boneSolveState.MeanSquareDistanceFrom(TransformQvvs.identity);
                    var newMsd = boneSolveState.MeanSquareDistanceFrom(new TransformQvvs(float3.zero, newRotation));
                    if (newMsd < oldMsd)
                    {
                        bone.rootRotation = math.normalize(math.mul(newRotation, bone.rootRotation));
                    }
                }

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

