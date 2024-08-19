using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Dragons.Authoring
{
    [DisallowMultipleComponent]
    public class DancerReferenceGroupHybridAuthoring : MonoBehaviour
    {
        public GameObject dancerGoPrefab;
    }

    class DancerReferenceGroupHybridBaker : Baker<DancerReferenceGroupHybridAuthoring>
    {
        public override void Bake(DancerReferenceGroupHybridAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new DancerReferenceGroupSourcePrefab
            {
                dancerGoPrefab    = authoring.dancerGoPrefab,
                bonesPerReference = 0
            });
        }
    }
}

