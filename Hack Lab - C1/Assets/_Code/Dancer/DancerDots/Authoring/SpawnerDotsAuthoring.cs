using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Dragons
{
    [DisallowMultipleComponent]
    public class SpawnerDotsAuthoring : MonoBehaviour
    {
        public GameObject dancerPrefab;
        public int        referencesToSpawn;
        public int        rows;
        public int        columns;
        public float      interval;
    }

    class SpawnerDotsBaker : Baker<SpawnerDotsAuthoring>
    {
        public override void Bake(SpawnerDotsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SpawnerDots
            {
                dancerPrefab      = GetEntity(authoring.dancerPrefab, TransformUsageFlags.Dynamic),
                referencesToSpawn = authoring.referencesToSpawn,
                rows              = authoring.rows,
                columns           = authoring.columns,
                interval          = authoring.interval
            });
        }
    }
}

