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
        public partial class TimerScript : UnikaScriptAuthoring<Scripts.TimerScript>
        {
            public float period = 5f;

            public override void Bake(IBaker baker, ref AuthoredScriptAssignment toAssign, Entity smartPostProcessTarget)
            {
                var script = new Scripts.TimerScript
                {
                    period                  = period,
                    timeUntilNextTransition = period,
                    currentState            = true
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
        public partial interface ITimeState : IUnikaInterface
        {
            bool GetState();
        }

        public partial struct TimerScript : IUnikaScript, IUnikaUpdate, ITimeState
        {
            public float period;
            public float timeUntilNextTransition;
            public bool  currentState;

            public void Update(ref UnikaUpdateContext context, Script thisScript)
            {
                timeUntilNextTransition -= context.deltaTime;
                if (timeUntilNextTransition < 0f)
                {
                    timeUntilNextTransition += period;
                    currentState             = !currentState;
                }
            }

            public bool GetState() => currentState;
        }
    }
}

