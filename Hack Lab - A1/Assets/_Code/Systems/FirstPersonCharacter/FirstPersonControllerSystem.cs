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
                // Todo: Does it feel better to apply rotation at the beginning or end?
                ApplyRotation(transform, ref state, in desiredActions);

                bool wasPreviouslyGrounded = state.isGrounded;
            }

            void ApplyRotation(TransformAspect transform, ref FirstPersonControllerState state, in FirstPersonDesiredActions desiredActions)
            {
                var deltaX              = desiredActions.lookDirectionFromForward.x;
                var deltaForward        = new float3(deltaX, 0f, math.sqrt(1f - deltaX * deltaX));
                var deltaRotation       = quaternion.LookRotation(deltaForward, math.up());
                state.velocity          = math.rotate(deltaRotation, state.velocity);
                transform.localRotation = math.mul(deltaRotation, transform.localRotation);
            }
        }
    }
}

