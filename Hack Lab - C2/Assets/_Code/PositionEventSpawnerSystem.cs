using Latios;
using Latios.LifeFX;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[RequireMatchingQueriesForUpdate]
[BurstCompile]
public partial struct PositionEventSpawnerSystem : ISystem
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
        var mailbox = latiosWorld.worldBlackboardEntity.GetCollectionComponent<GraphicsEventPostal>(true).GetMailbox<float3>();
        foreach ((var transform, var spawner) in SystemAPI.Query<WorldTransform, RefRW<PositionEventSpawner> >())
        {
            ref var sp             = ref spawner.ValueRW;
            sp.timeUntilNextSpawn -= SystemAPI.Time.DeltaTime;
            if (sp.timeUntilNextSpawn < 0f)
            {
                sp.timeUntilNextSpawn += sp.spawnPeriod;
                mailbox.Send(transform.position, sp.tunnel);
            }
        }
    }
}

