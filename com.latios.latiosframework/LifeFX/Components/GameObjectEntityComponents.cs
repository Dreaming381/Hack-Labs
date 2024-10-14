using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.LifeFX
{
    // GameObjectEntity entities tend to be very small in size. Therefore, we can splurge a little on the DynamicBuffer.
    // This consumes 64 bytes.
    [InternalBufferCapacity(4)]
    internal struct GraphicsEventTunnelDestination : IBufferElementData
    {
        public UnityObjectRef<GraphicsEventTunnelBase> tunnel;
        public UnityObjectRef<GraphicsEventRequestor>  requestor;
        public int                                     eventTypeIndex;
    }
}

