﻿namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    [Flags]
    internal enum PlaneSide
    {
        None = 0,
        Front = 1,
        Back = 2,
        Both = Front | Back,
        Facing = 4,
    }
}
