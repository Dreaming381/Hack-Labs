using Latios;
using Latios.Transforms;
using Latios.Transforms.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace C3
{
    [UpdateBefore(typeof(TransformSuperSystem))]
    public partial class PreTransformsRootSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<UnikaUpdateSystem>();
        }
    }
}

