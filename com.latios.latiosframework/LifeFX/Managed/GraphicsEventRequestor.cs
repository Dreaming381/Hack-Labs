using Latios.Transforms;
using Unity.Entities;
using UnityEngine;

namespace Latios.LifeFX
{
    public class GraphicsEventRequestor : MonoBehaviour, IInitializeGameObjectEntity
    {
        [SerializeField] private GraphicsEventTunnelBase tunnel;

        // Todo: Change this to UnityEvent?
        public delegate void OnGraphicsEventPublishedDelegate(GraphicsBuffer graphicsBuffer, int startIndex, int count);

        public event OnGraphicsEventPublishedDelegate OnGraphicsEventPublished;

        public void Initialize(LatiosWorld latiosWorld, Entity gameObjectEntity)
        {
            DynamicBuffer<GraphicsEventTunnelDestination> buffer;
            if (latiosWorld.EntityManager.HasBuffer<GraphicsEventTunnelDestination>(gameObjectEntity))
                buffer = latiosWorld.EntityManager.GetBuffer<GraphicsEventTunnelDestination>(gameObjectEntity);
            else
                buffer = latiosWorld.EntityManager.AddBuffer<GraphicsEventTunnelDestination>(gameObjectEntity);
            buffer.Add(new GraphicsEventTunnelDestination
            {
                tunnel         = tunnel,
                requestor      = this,
                eventTypeIndex = tunnel.GetEventIndex(),
            });
        }

        internal void Publish(GraphicsBuffer graphicsBuffer, int startIndex, int count) => OnGraphicsEventPublished(graphicsBuffer, startIndex, count);
    }
}

