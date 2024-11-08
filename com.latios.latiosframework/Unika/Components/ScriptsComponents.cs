using Unity.Collections;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using Unity.Mathematics;

namespace Latios.Unika
{
    /// <summary>
    /// The base interface any Unika interface should explicitly specify.
    /// </summary>
    public interface IUnikaInterface
    {
    }

    /// <summary>
    /// The base interface any Unika script should explicitly specify.
    /// </summary>
    public interface IUnikaScript
    {
    }

    /// <summary>
    /// The container of all scripts attached to an entity.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct UnikaScripts : IBufferElementData
    {
        internal ScriptHeader header;
    }

    /// <summary>
    /// All entities within all scripts can be serialized and deserialized from here to support entity remapping.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct UnikaSerializedEntityReference : IBufferElementData
    {
        internal Entity entity;
        internal int    byteOffset;
    }

    /// <summary>
    /// The serialized blob assets for scripts in a subscene. You only need to worry about this if you have a custom save system.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct UnikaSerializedBlobReference : IBufferElementData
    {
        internal UnsafeUntypedBlobAssetReference blob;
        internal int                             byteOffset;
    }

    /// <summary>
    /// The serialized asset references for scripts in a subscene. You only need to worry about this if you have a custom save system.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct UnikaSerializedAssetReference : IBufferElementData
    {
        internal UntypedWeakReferenceId asset;
        internal int                    byteOffset;
    }

    /// <summary>
    /// The serialized UnityObjectRefs for scripts in a subscene. You only need to worry about this if you have a custom save system.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct UnikaSerializedObjectReference : IBufferElementData
    {
        internal UnityObjectRef<UnityEngine.Object> obj;
        internal int                                byteOffset;
    }

    /// <summary>
    /// The serialized script type IDs for scripts in a subscene to remap from baked types to runtime types. You only need to worry about this if you have a custom save system.
    /// </summary>
    public struct UnikaSerializedTypeIds : IComponentData
    {
        internal BlobAssetReference<UnikaSerializedTypeIdsBlob> blob;
    }

    public struct UnikaSerializedTypeIdsBlob
    {
        public BlobArray<ulong> stableHashBySerializedTypeId;
    }
}

