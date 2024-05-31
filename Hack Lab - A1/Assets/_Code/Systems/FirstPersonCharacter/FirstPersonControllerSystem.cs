using Latios;
using Latios.Psyshock;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
                var initialGroundCheckResult = CheckGround(startingPosition, initialGroundCheckDistance, in stats, state.accumulatedJumpTime);

                // Todo: Does it feel better to apply rotation now or at the end?
                ApplyRotation(transform, in desiredActions, initialGroundCheckResult.groundFound);
                var velA = state.velocity;
                ApplyJump(desiredActions.jump, ref state, in stats);
                var velB = state.velocity;
                if (state.accumulatedJumpTime > 0f)
                    initialGroundCheckResult.groundFound = false;
                ApplyGravity(initialGroundCheckResult.groundFound, ref state, in stats);
                var velC = state.velocity;
                ApplyMoveInput(desiredActions.move, transform.worldRotation, in initialGroundCheckResult, ref state.velocity, in stats);
                var velD = state.velocity;
                CollideAndSlide(startingPosition, ref state.velocity, in stats);
                var velE                    = state.velocity;
                var collideAndSlidePosition = startingPosition + state.velocity * deltaTime;

                var afterMoveGroundCheckDistance = stats.targetHoverHeight + math.select(stats.extraGroundCheckDistanceWhileInAir,
                                                                                         stats.extraGroundCheckDistanceWhileGrounded,
                                                                                         initialGroundCheckResult.groundFound);
                var afterMoveGroundCheckResult = CheckGround(collideAndSlidePosition, afterMoveGroundCheckDistance, in stats, state.accumulatedJumpTime);
                if (afterMoveGroundCheckResult.groundFound)
                {
                    //if (afterMoveGroundCheckResult.distance < stats.targetHoverHeight)
                    {
                        var targetY = collideAndSlidePosition.y - afterMoveGroundCheckResult.distance + stats.targetHoverHeight;
                        ApplySpring(ref state.velocity, collideAndSlidePosition, targetY, in stats);
                    }
                }
                var velF = state.velocity;
                if (!math.all(math.isfinite(velF)))
                    UnityEngine.Debug.Log($"velocity broke: a: {velA}, b: {velB}, c: {velC}, d: {velD}, e: {velE}, f: {velF}");
                state.isGrounded        = afterMoveGroundCheckResult.groundFound;
                transform.worldPosition = startingPosition + state.velocity * deltaTime;
            }

            void ApplyRotation(TransformAspect transform, in FirstPersonDesiredActions desiredActions, bool grounded)
            {
                var deltaX              = desiredActions.lookDirectionFromForward.x;
                var deltaForward        = new float3(deltaX, 0f, math.sqrt(1f - deltaX * deltaX));
                var deltaRotation       = quaternion.LookRotation(deltaForward, math.up());
                transform.localRotation = math.mul(deltaRotation, transform.localRotation);
            }

            struct CheckGroundResult
            {
                public float3 normal;
                public float  distance;
                public bool   groundFound;
            }

            unsafe CheckGroundResult CheckGround(float3 start, float checkDistance, in FirstPersonControllerStats stats, float accumulatedJumpTime)
            {
                if (accumulatedJumpTime > 0f)
                    return default;

                start.y                 += stats.capsuleRadius + stats.skinWidth;
                checkDistance           += stats.skinWidth;
                Collider sphere          = new SphereCollider(float3.zero, stats.capsuleRadius);
                var      startTransform  = new TransformQvvs(start, quaternion.identity);
                var      end             = start;
                end.y                   -= checkDistance;
                if (Physics.ColliderCast(in sphere, in startTransform, end, in staticEnvironmentCollisionLayer, out var result, out var bodyInfo))
                {
                    startTransform.position.y -= result.distance;
                    var accumulatedNormal      = -result.normalOnCaster;
                    var processor              = new CheckGroundProcessor
                    {
                        testSphere              = sphere,
                        testSphereTransform     = startTransform,
                        accumulatedNormal       = &accumulatedNormal,
                        firstHitBodyIndex       = bodyInfo.bodyIndex,
                        firstHitBodySubcollider = result.subColliderIndexOnTarget,
                        skinWidth               = stats.skinWidth
                    };
                    var aabb  = Physics.AabbFrom(sphere, startTransform);
                    aabb.min -= stats.skinWidth;
                    aabb.max += stats.skinWidth;
                    Physics.FindObjects(aabb, in staticEnvironmentCollisionLayer, processor).RunImmediate();
                    var finalNormal = math.normalizesafe(accumulatedNormal, math.down());

                    return new CheckGroundResult
                    {
                        distance    = result.distance,
                        normal      = finalNormal,
                        groundFound = finalNormal.y >= stats.minSlopeY
                    };
                }
                return default;
            }

            unsafe struct CheckGroundProcessor : IDistanceBetweenAllProcessor, IFindObjectsProcessor
            {
                public Collider      testSphere;
                public TransformQvvs testSphereTransform;
                public float3*       accumulatedNormal;
                public int           firstHitBodySubcollider;
                public int           firstHitBodyIndex;
                public float         skinWidth;
                bool                 checkSubcollider;

                public void Execute(in ColliderDistanceResult result)
                {
                    if (!checkSubcollider || result.subColliderIndexB != firstHitBodySubcollider)
                        *accumulatedNormal -= result.normalA;
                }

                public void Execute(in FindObjectsResult result)
                {
                    checkSubcollider = result.bodyIndex == firstHitBodyIndex;
                    Physics.DistanceBetweenAll(in testSphere, in testSphereTransform, result.collider, result.transform, skinWidth, ref this);
                }
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

            void ApplyJump(bool jump, ref FirstPersonControllerState state, in FirstPersonControllerStats stats)
            {
                if (state.isGrounded)
                    state.accumulatedCoyoteTime  = 0f;
                state.accumulatedCoyoteTime     += deltaTime;
                if (jump && state.accumulatedCoyoteTime <= stats.coyoteTime)
                {
                    state.velocity.y          += stats.jumpVelocity;
                    state.accumulatedJumpTime  = math.EPSILON;
                }
                else if (state.accumulatedJumpTime > 0f)
                    state.accumulatedJumpTime += deltaTime;
                if (state.accumulatedJumpTime > stats.jumpInitialMaxTime || (!jump && state.accumulatedJumpTime >= stats.jumpInitialMinTime))
                    state.accumulatedJumpTime  = 0f;
                state.isGrounded              &= state.accumulatedJumpTime > 0f;
            }

            void ApplyGravity(bool isGrounded, ref FirstPersonControllerState state, in FirstPersonControllerStats stats)
            {
                if (!isGrounded)
                {
                    var gravity = stats.fallGravity;
                    if (state.accumulatedJumpTime >= stats.jumpInitialMaxTime)
                        gravity = stats.jumpGravity;
                    else if (state.accumulatedJumpTime > 0f)
                        gravity       = stats.jumpInitialGravity;
                    state.velocity.y -= gravity * deltaTime;
                }
                state.velocity.y = math.max(state.velocity.y, -stats.maxFallSpeed);
            }

            void ApplyMoveInput(float2 move, quaternion rotation, in CheckGroundResult checkGroundResult, ref float3 velocity, in FirstPersonControllerStats stats)
            {
                if (!checkGroundResult.groundFound)
                {
                    var forwardVelocity = math.dot(math.forward(rotation), velocity);
                    var rightVelocity   = math.dot(math.rotate(rotation, math.right()), velocity);
                    var newVelocities   = Physics.StepVelocityWithInput(move.yx,
                                                                        new float2(forwardVelocity, rightVelocity),
                                                                        new float2(stats.airStats.forwardAcceleration, stats.airStats.strafeAcceleration),
                                                                        new float2(stats.airStats.forwardDeceleration, stats.airStats.strafeDeceleration),
                                                                        new float2(stats.airStats.forwardTopSpeed,     stats.airStats.strafeTopSpeed),
                                                                        new float2(stats.airStats.reverseAcceleration, stats.airStats.strafeAcceleration),
                                                                        new float2(stats.airStats.reverseDeceleration, stats.airStats.strafeDeceleration),
                                                                        new float2(stats.airStats.reverseTopSpeed,     stats.airStats.strafeTopSpeed),
                                                                        deltaTime);
                    velocity.zx = math.rotate(rotation, newVelocities.yx.x0y()).zx;
                }
                else
                {
                    var forward                  = math.mul(quaternion.LookRotation(-checkGroundResult.normal, math.forward(rotation)), math.up());
                    var right                    = math.mul(quaternion.LookRotation(-checkGroundResult.normal, math.rotate(rotation, math.right())), math.up());
                    var slopeCompensatedRotation = quaternion.LookRotation(forward, math.cross(forward, right));
                    var slopeHeadingVelocity     = math.InverseRotateFast(slopeCompensatedRotation, velocity);
                    if (math.length(velocity.xz) < math.EPSILON)
                        slopeHeadingVelocity.xz = 0f; // This prevents getting stuck when the spring is driving the velocity on a slope.
                    slopeHeadingVelocity.zx     = Physics.StepVelocityWithInput(move.yx,
                                                                                slopeHeadingVelocity.zx,
                                                                                new float2(stats.walkStats.forwardAcceleration, stats.walkStats.strafeAcceleration),
                                                                                new float2(stats.walkStats.forwardDeceleration, stats.walkStats.strafeDeceleration),
                                                                                new float2(stats.walkStats.forwardTopSpeed,     stats.walkStats.strafeTopSpeed),
                                                                                new float2(stats.walkStats.reverseAcceleration, stats.walkStats.strafeAcceleration),
                                                                                new float2(stats.walkStats.reverseDeceleration, stats.walkStats.strafeDeceleration),
                                                                                new float2(stats.walkStats.reverseTopSpeed,     stats.walkStats.strafeTopSpeed),
                                                                                deltaTime);
                    velocity = math.rotate(slopeCompensatedRotation, slopeHeadingVelocity);
                }
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
                    if (distanceRemaining < math.EPSILON)
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

