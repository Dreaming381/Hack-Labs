using Latios;
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
    public partial struct FirstPersonAimVerticalSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new Job { desiredActionsLookup = GetComponentLookup<FirstPersonDesiredActions>(true) }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            [ReadOnly] public ComponentLookup<FirstPersonDesiredActions> desiredActionsLookup;

            public void Execute(TransformAspect transform, in FirstPersonVerticalAimStats stats)
            {
                ref readonly var actions = ref desiredActionsLookup.GetRefRO(stats.actionsEntity).ValueRO;

                var deltaY             = math.clamp(actions.lookDirectionFromForward.y, -0.9f, 0.9f);
                var deltaForward       = new float3(0f, deltaY, math.sqrt(1f - deltaY * deltaY));
                var deltaRotation      = quaternion.LookRotation(deltaForward, math.up());
                var newRotation        = math.mul(deltaRotation, transform.localRotation);
                var newForwardY        = math.forward(newRotation).y;
                var clampedNewForwardY = math.clamp(newForwardY, stats.minSinLimit, stats.maxSinLimit);
                if (newForwardY != clampedNewForwardY)
                {
                    // We hit the boundary and need to clamp the rotation.
                    var clampedForward = new float3(0f, clampedNewForwardY, math.sqrt(1f - clampedNewForwardY * clampedNewForwardY));
                    newRotation        = quaternion.LookRotation(clampedForward, math.up());
                }
                transform.localRotation = newRotation;
            }
        }
    }
}

