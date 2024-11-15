using Latios.Transforms;
using Latios.Unika;
using Latios.Unika.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace C3
{
    namespace Authoring
    {
        public partial class SineScript : UnikaScriptAuthoring<Scripts.SineScript>
        {
            public float                                     frequency = 0.5f;
            public float                                     amplitude = 1.5f;
            public UnikaScriptAuthoring<Scripts.TimerScript> timer;

            public override void Bake(IBaker baker, ref AuthoredScriptAssignment toAssign, Entity smartPostProcessTarget)
            {
                var script = new Scripts.SineScript
                {
                    freqRadians = frequency * math.TAU,
                    amplitude   = amplitude,
                    currentTime = 0f,
                    timerRef    = baker.GetScriptRefOrDefaultFrom(timer),
                };
                toAssign.Assign(ref script);
                toAssign.transformUsageFlags = TransformUsageFlags.None;
                toAssign.userFlagA           = false;
                toAssign.userFlagB           = false;
                toAssign.userByte            = 0;
            }

            public override bool IsValid()
            {
                return true;
            }
        }
    }

    namespace Scripts
    {
        public partial struct SineScript : IUnikaScript, IUnikaUpdate
        {
            public float                  freqRadians;
            public float                  amplitude;
            public float                  currentTime;
            public ScriptRef<TimerScript> timerRef;

            public void Update(ref UnikaUpdateContext context, Script thisScript)
            {
                if (!timerRef.TryResolve(thisScript.allScripts, out var timer))
                {
                    UnityEngine.Debug.LogWarning("Failed to resolve timer.");
                    return;
                }

                // Test return values of interfaces.
                ITimeState.Interface timerface = timer.ToInterface();
                bool                 state     = timerface.GetState();
                if (state)
                {
                    currentTime += context.deltaTime;
                    context.ecs.TryGetTransformAspect(thisScript.entity, out var transform);
                    var worldPosition       = transform.worldPosition;
                    worldPosition.y         = amplitude * math.sin(currentTime * freqRadians);
                    transform.worldPosition = worldPosition;
                }
            }
        }
    }
}

