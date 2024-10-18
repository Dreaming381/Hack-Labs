using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PositionEventSpawnerAuthoring : MonoBehaviour
{
    public float                       period = 1f;
    public PositionGraphicsEventTunnel tunnel;
}

public class PositionEventSpawnerAuthoringBaker : Baker<PositionEventSpawnerAuthoring>
{
    public override void Bake(PositionEventSpawnerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Renderable);
        AddComponent(entity, new PositionEventSpawner
        {
            spawnPeriod = authoring.period,
            tunnel      = authoring.tunnel
        });
    }
}

