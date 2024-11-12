using Latios.Transforms;
using Latios.Unika;
using Latios.Unika.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace C3.Authoring
{
    public class SpinningScript : UnikaScriptAuthoring<Scripts.SpinningScript>
    {
        public float rotationSpeed = 135f;

        public override void Bake(IBaker baker, ref AuthoredScriptAssignment toAssign, Entity smartPostProcessTarget)
        {
            var script = new Scripts.SpinningScript { rotationSpeedRadians = math.radians(rotationSpeed) };
            toAssign.Assign(ref script);
            toAssign.transformUsageFlags = TransformUsageFlags.Dynamic;
        }

        public override bool IsValid()
        {
            return true;
        }
    }
}

namespace C3.Scripts
{
    public partial struct SpinningScript : IUnikaScript, IUnikaUpdate
    {
        public float rotationSpeedRadians;

        public void Update(ref UnikaUpdateContext context, Script thisScript)
        {
            context.ecs.TryGetTransformAspect(thisScript.entity, out var transform);
            transform.RotateLocal(quaternion.Euler(0f, rotationSpeedRadians * context.deltaTime, 0f));
        }
    }
}

