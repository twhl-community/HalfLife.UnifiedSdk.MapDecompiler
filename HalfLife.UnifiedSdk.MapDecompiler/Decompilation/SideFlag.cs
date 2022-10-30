namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    [Flags]
    internal enum SideFlag : ushort
    {
        None = 0,
        Tested = 1,
        Visible = 2,
        Bevel = 4,
        Textured = 8,
        Curve = 16,
    }
}
