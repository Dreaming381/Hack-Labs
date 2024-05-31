using Latios;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace A1
{
    public struct FirstPersonDesiredActions : IComponentData
    {
        public float2 lookDirectionFromForward;  // Assume z = sqrt(1 - lengthSq)
        public float2 move;
        public bool   jump;
    }

    public struct FirstPersonVerticalAimStats : IComponentData
    {
        public EntityWith<FirstPersonDesiredActions> actionsEntity;
        public float                                 minSinLimit;
        public float                                 maxSinLimit;
    }

    public struct FirstPersonControllerStats : IComponentData
    {
        public struct MovementStats
        {
            public float forwardTopSpeed;
            public float reverseTopSpeed;
            public float strafeTopSpeed;

            public float forwardAcceleration;
            public float forwardDeceleration;
            public float reverseAcceleration;
            public float reverseDeceleration;
            public float strafeAcceleration;
            public float strafeDeceleration;
        }

        public MovementStats walkStats;
        public MovementStats airStats;

        public float capsuleRadius;
        public float capsuleHeight;
        public float skinWidth;
        public float targetHoverHeight;
        public float extraGroundCheckDistanceWhileGrounded;
        public float extraGroundCheckDistanceWhileInAir;
        public float minSlopeY;

        public float springFrequency;
        public float springDampingRatio;

        public float jumpVelocity;
        public float jumpInitialGravity;
        public float jumpGravity;
        public float jumpInitialMinTime;
        public float jumpInitialMaxTime;
        public float fallGravity;
        public float maxFallSpeed;
        public float coyoteTime;
    }

    public struct FirstPersonControllerState : IComponentData
    {
        public float3 velocity;
        public float  accumulatedCoyoteTime;
        public float  accumulatedJumpTime;
        public bool   isGrounded;
    }
}

