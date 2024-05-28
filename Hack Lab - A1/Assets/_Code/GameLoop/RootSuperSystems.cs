using Latios;
using Latios.Transforms;
using Latios.Transforms.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace A1
{
    [UpdateBefore(typeof(TransformSuperSystem))]
    public partial class PreTransformsSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<BuildStaticEnvironmentCollisionLayerSystem>();
            GetOrCreateAndAddManagedSystem<ReadPlayerInputSystem>();
            GetOrCreateAndAddUnmanagedSystem<FirstPersonControllerSystem>();
            GetOrCreateAndAddUnmanagedSystem<FirstPersonAimVerticalSystem>();
        }
    }
}

