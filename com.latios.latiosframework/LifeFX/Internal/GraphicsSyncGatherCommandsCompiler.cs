using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.LifeFX
{
    internal struct GraphicsSyncCopyOp
    {
        public enum OpType : byte
        {
            ByteRange,
            BitRange,
            SyncExist,
            ComponentExist,
            ComponentEnabled
        }

        public byte   indexInTypeHandles;
        public OpType opType;
        public short  dstStart;
        public short  srcStart;
        public short  count;
    }

    internal struct GraphicsSyncInstructionsForType
    {
        public bool                          isEnableable;
        public bool                          requiresOrderVersionCheck;
        public short                         typeSize;
        public short                         stateFieldOffset;
        public short                         bufferElementTypeSize;
        public BlobArray<TypeIndex>          changeFilterTypes;
        public BlobArray<GraphicsSyncCopyOp> copyOps;
        public UnityObjectRef<ComputeShader> uploadShader;
        public int                           shaderPropertyID;
    }

    internal struct GraphicsSyncInstructionsBlob
    {
        public BlobArray<GraphicsSyncInstructionsForType> instructionsByType;
        public BlobArray<TypeIndex>                       typeIndicesForTypeHandles;
    }

    internal static class GraphicsSyncGatherCommandsCompiler
    {
        static BlobAssetReference<GraphicsSyncInstructionsBlob> s_blob = default;
        static List<ComputeShader>                              s_computeShaders;  // GC defeat

        public static void Init()
        {
            if (s_blob.IsCreated)
                return;

            CompileAllTypes();

            // important: this will always be called from a special unload thread (main thread will be blocking on this)
            AppDomain.CurrentDomain.DomainUnload += (_, __) => { Shutdown(); };

            // There is no domain unload in player builds, so we must be sure to shutdown when the process exits.
            AppDomain.CurrentDomain.ProcessExit += (_, __) => { Shutdown(); };
        }

        static void Shutdown()
        {
            if (s_blob.IsCreated)
                s_blob.Dispose();
            s_computeShaders.Clear();
            s_computeShaders = null;
        }

        static void CompileAllTypes()
        {
            var typesToCompile = new UnsafeList<TypeIndex>(128, Allocator.Temp);
            var baseInterface  = typeof(IGraphicsSyncComponentBase);

            foreach (var componentType in TypeManager.AllTypes)
            {
                if (componentType.IsZeroSized || componentType.BakingOnlyType || componentType.TemporaryBakingType)
                    continue;
                var typeIndex = componentType.TypeIndex;
                if (!typeIndex.IsComponentType || typeIndex.IsManagedType)
                    continue;

                var type = componentType.Type;
                if (!baseInterface.IsAssignableFrom(type))
                    continue;

                typesToCompile.Add(typeIndex);
            }

            if (typesToCompile.Length > 255)
                throw new System.InvalidOperationException("There are too many IGraphicsSyncComponent<> types in the project.");

            s_computeShaders = new List<ComputeShader>();

            BlobBuilder builder            = new BlobBuilder(Allocator.Temp);
            ref var     root               = ref builder.ConstructRoot<GraphicsSyncInstructionsBlob>();
            var         instructionsByType = builder.Allocate(ref root.instructionsByType, typesToCompile.Length);

            var  typeIndicesToHandleIndices = new NativeHashMap<TypeIndex, byte>(256, Allocator.Temp);
            byte counter                    = 0;
            foreach (var typeIndex in typesToCompile)
            {
                typeIndicesToHandleIndices.Add(typeIndex, counter);
                counter++;
            }

            for (counter = 0; counter < typesToCompile.Length; counter++)
                CompileType(ref instructionsByType[counter], typeIndicesToHandleIndices);

            var handles = builder.Allocate(ref root.typeIndicesForTypeHandles, typeIndicesToHandleIndices.Count);
            foreach (var indices in typeIndicesToHandleIndices)
                handles[indices.Value] = indices.Key;

            s_blob = builder.CreateBlobAssetReference<GraphicsSyncInstructionsBlob>(Allocator.Persistent);
        }

        static void CompileType(ref GraphicsSyncInstructionsForType instructionsForType, NativeHashMap<TypeIndex, byte> typeIndicesToHandleIndices)
        {
            // Todo
        }
    }
}

