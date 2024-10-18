using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

struct PositionEventSpawner : IComponentData
{
    public float                                       timeUntilNextSpawn;
    public float                                       spawnPeriod;
    public UnityObjectRef<PositionGraphicsEventTunnel> tunnel;
}

