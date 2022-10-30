﻿using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    internal sealed class DecompiledEntity
    {
        public readonly int Index;

        public readonly Entity Entity;

        public DecompiledEntity(int index, Entity entity)
        {
            Index = index;
            Entity = entity;
        }
    }
}
