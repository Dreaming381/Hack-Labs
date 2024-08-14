using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Dragons
{
    [DisallowMultipleComponent]
    public class DancerDotsAuthoring : MonoBehaviour
    {
        public Transform leftFoot;
        public Transform rightFoot;
        public float     offset;
    }

    //class DancerDotsBaker : Baker<DancerDotsAuthoring>
    //{
    //    public override void Bake(DancerDotsAuthoring authoring)
    //    {
    //        var entity = GetEntity(TransformUsageFlags.Dynamic);
    //        // Todo: Foot correction
    //        //AddComponent<DancerFootCache>(entity);
    //    }
    //}
}

