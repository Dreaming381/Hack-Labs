using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace A1
{
    public class EnvironmentCollisionAuthoring : MonoBehaviour
    {
        public enum Mode
        {
            IncludeRecursively,
            ExcludeRecursively
        }

        public Mode mode;
    }

    public class EnvironmentCollisionAuthoringBaker : Baker<Collider>
    {
        static List<Collider> s_colliderCache = new List<Collider>();

        public override void Bake(Collider authoring)
        {
            s_colliderCache.Clear();
            GetComponents(s_colliderCache);
            if (s_colliderCache[0] != authoring)
                return;

            var ancencestor = GetComponentInParent<EnvironmentCollisionAuthoring>();
            if (ancencestor == null)
                return;
            if (ancencestor.mode == EnvironmentCollisionAuthoring.Mode.IncludeRecursively)
            {
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent<StaticEnvironmentTag>(entity);
            }
        }
    }
}

