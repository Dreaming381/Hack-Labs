using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace A1
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct FirstPersonControllerSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new Job
            {
                staticEnvironmentCollisionLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<StaticEnvironmentCollisionLayer>().layer,
                deltaTime                       = Time.DeltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            [ReadOnly] public CollisionLayer staticEnvironmentCollisionLayer;

            public float deltaTime;

            public void Execute(TransformAspect transform, ref FirstPersonControllerState state, in FirstPersonControllerStats stats, in FirstPersonDesiredActions desiredActions)
            {
                bool   wasPreviouslyGrounded = state.isGrounded;
                float3 startingPosition      = transform.worldPosition;

                var initialGroundCheckDistance = stats.targetHoverHeight + math.select(stats.extraGroundCheckDistanceWhileInAir,
                                                                                       stats.extraGroundCheckDistanceWhileGrounded,
                                                                                       wasPreviouslyGrounded);
                var initialGroundCheckResult = CheckGround(startingPosition, initialGroundCheckDistance, in stats);

                // Todo: Does it feel better to apply rotation now or at the end?
                ApplyRotation(transform, ref state, in desiredActions, initialGroundCheckResult.groundFound);

                if (!initialGroundCheckResult.groundFound)
                {
                    state.velocity.y -= stats.fallGravity * deltaTime;
                }

                var collideAndSlidePosition = startingPosition;
                // This is where we will do collide-n-slide
                // Make sure to update velocity afterwards (PBD style), the following line is temporary
                collideAndSlidePosition += state.velocity * deltaTime;

                var afterMoveGroundCheckDistance = stats.targetHoverHeight + math.select(stats.extraGroundCheckDistanceWhileInAir,
                                                                                         stats.extraGroundCheckDistanceWhileGrounded,
                                                                                         initialGroundCheckResult.groundFound);
                var afterMoveGroundCheckResult = CheckGround(collideAndSlidePosition, afterMoveGroundCheckDistance, in stats);
                if (afterMoveGroundCheckResult.groundFound)
                {
                    //if (afterMoveGroundCheckResult.distance < stats.targetHoverHeight)
                    {
                        var targetY = collideAndSlidePosition.y - afterMoveGroundCheckResult.distance + stats.targetHoverHeight;
                        ApplySpring(ref state.velocity, collideAndSlidePosition, targetY, in stats);
                    }
                }
                state.isGrounded        = afterMoveGroundCheckResult.groundFound;
                transform.worldPosition = startingPosition + state.velocity * deltaTime;
            }

            void ApplyRotation(TransformAspect transform, ref FirstPersonControllerState state, in FirstPersonDesiredActions desiredActions, bool grounded)
            {
                var deltaX              = desiredActions.lookDirectionFromForward.x;
                var deltaForward        = new float3(deltaX, 0f, math.sqrt(1f - deltaX * deltaX));
                var deltaRotation       = quaternion.LookRotation(deltaForward, math.up());
                transform.localRotation = math.mul(deltaRotation, transform.localRotation);
                if (grounded)
                    state.velocity = math.rotate(deltaRotation, state.velocity);
            }

            struct CheckGroundResult
            {
                public float3 normal;
                public float  distance;
                public bool   groundFound;
            }

            CheckGroundResult CheckGround(float3 start, float checkDistance, in FirstPersonControllerStats stats)
            {
                start.y                 += stats.capsuleRadius + stats.skinWidth;
                checkDistance           += stats.skinWidth;
                Collider sphere          = new SphereCollider(float3.zero, stats.capsuleRadius);
                var      startTransform  = new TransformQvvs(start, quaternion.identity);
                var      end             = start;
                end.y                   -= checkDistance;
                if (Physics.ColliderCast(in sphere, in startTransform, end, in staticEnvironmentCollisionLayer, out var result, out _))
                {
                    return new CheckGroundResult
                    {
                        distance    = result.distance,
                        normal      = -result.normalOnCaster,
                        groundFound = true
                    };
                }
                return default;
            }

            void ApplySpring(ref float3 velocity, float3 currentPosition, float targetY, in FirstPersonControllerStats stats)
            {
                UnitySim.ConstraintTauAndDampingFrom(stats.springFrequency, stats.springDampingRatio, deltaTime, 1, out var tau, out var damping);
                var inertialA = new RigidTransform(quaternion.identity, currentPosition);
                var inertialB = new RigidTransform(quaternion.identity, new float3(currentPosition.x, targetY, currentPosition.z));
                UnitySim.BuildJacobian(out UnitySim.PositionConstraintJacobianParameters parameters, inertialA, float3.zero,
                                       inertialB, RigidTransform.identity, 0f, 0f, tau, damping, new bool3(false, true, false));
                var               simVelocity   = new UnitySim.Velocity { linear = velocity, angular = float3.zero };
                UnitySim.Velocity dummyVelocity                                                      = default;
                // The inverse mass will cancel itself out when the impulse is applied to the velocity. So we just pass 1f here.
                UnitySim.SolveJacobian(ref simVelocity, inertialA, new UnitySim.Mass { inverseMass = 1f, inverseInertia = float3.zero },
                                       ref dummyVelocity, inertialB, default,
                                       in parameters, deltaTime, 1f / deltaTime);
                velocity = simVelocity.linear;
            }
        }
    }
}

