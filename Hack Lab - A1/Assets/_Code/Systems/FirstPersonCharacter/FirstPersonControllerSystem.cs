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

                ApplyGravity(initialGroundCheckResult.groundFound, ref state.velocity, in stats);
                ApplyMoveInput(desiredActions.move, transform.worldRotation, initialGroundCheckResult.groundFound, ref state.velocity, in stats);
                CollideAndSlide(startingPosition, ref state.velocity, in stats);
                var collideAndSlidePosition = startingPosition + state.velocity * deltaTime;

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
                //if (grounded)
                //    state.velocity = math.rotate(deltaRotation, state.velocity);
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

            void ApplyGravity(bool isGrounded, ref float3 velocity, in FirstPersonControllerStats stats)
            {
                if (!isGrounded)
                {
                    velocity.y -= stats.fallGravity * deltaTime;
                }
                velocity.y = math.max(velocity.y, -stats.maxFallSpeed);
            }

            void ApplyMoveInput(float2 move, quaternion rotation, bool isGrounded, ref float3 velocity, in FirstPersonControllerStats stats)
            {
                var moveStats       = isGrounded ? stats.walkStats : stats.airStats;
                var forwardVelocity = math.dot(math.forward(rotation), velocity);
                var rightVelocity   = math.dot(math.rotate(rotation, math.right()), velocity);
                var newVelocities   = Physics.StepVelocityWithInput(move.yx,
                                                                    new float2(forwardVelocity, rightVelocity),
                                                                    new float2(moveStats.forwardAcceleration, moveStats.strafeAcceleration),
                                                                    new float2(moveStats.forwardDeceleration, moveStats.strafeDeceleration),
                                                                    new float2(moveStats.forwardTopSpeed, moveStats.strafeTopSpeed),
                                                                    new float2(moveStats.reverseAcceleration, moveStats.strafeAcceleration),
                                                                    new float2(moveStats.reverseDeceleration, moveStats.strafeDeceleration),
                                                                    new float2(moveStats.reverseTopSpeed, moveStats.strafeTopSpeed),
                                                                    deltaTime);
                velocity.zx = math.rotate(rotation, newVelocities.yx.x0y()).zx;
            }

            void CollideAndSlide(float3 startPosition, ref float3 velocity, in FirstPersonControllerStats stats)
            {
                var      moveVector        = velocity * deltaTime;
                var      distanceRemaining = math.length(moveVector);
                var      currentTransform  = new TransformQvvs(startPosition, quaternion.identity);
                var      moveDirection     = math.normalize(moveVector);
                Collider collider          = new CapsuleCollider(new float3(0f, stats.capsuleRadius, 0f),
                                                                 new float3(0f, stats.capsuleHeight - stats.capsuleRadius, 0f),
                                                                 stats.capsuleRadius);

                for (int iteration = 0; iteration < 32; iteration++)
                {
                    if (distanceRemaining < 0f)
                        break;
                    var end = currentTransform.position + moveDirection * distanceRemaining;
                    if (Physics.ColliderCast(in collider, in currentTransform, end, in staticEnvironmentCollisionLayer, out var hitInfo, out _))
                    {
                        currentTransform.position += moveDirection * (hitInfo.distance - stats.skinWidth);
                        distanceRemaining         -= hitInfo.distance;
                        if (math.dot(hitInfo.normalOnTarget, moveDirection) < -0.9f) // If the obstacle directly opposes our movement
                            break;
                        // LookRotation corrects an "up" vector to be perpendicular to the "forward" vector. We cheat this to get a new moveDirection perpendicular to the normal.
                        moveDirection = math.mul(quaternion.LookRotation(hitInfo.normalOnCaster, moveDirection), math.up());
                    }
                    else
                    {
                        currentTransform.position += moveDirection * distanceRemaining;
                        distanceRemaining          = 0f;
                    }
                }
                velocity = (currentTransform.position - startPosition) / deltaTime;
                if (math.length(currentTransform.position - startPosition) < math.EPSILON)
                    velocity = float3.zero;
            }
        }
    }
}

