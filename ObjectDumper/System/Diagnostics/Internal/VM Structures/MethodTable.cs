using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
unsafe struct MethodTable
{
    [FieldOffset(0x00)] public ushort ComponentSize;
    [FieldOffset(0x00)] uint flags;
    [FieldOffset(0x04)] public uint BaseSize;
    [FieldOffset(0x10)] public MethodTable* ParentMethodTable;
    [FieldOffset(0x18)] public void* Module;
    [FieldOffset(0x28)] nint canonMT;
    [FieldOffset(0x30)] public unsafe void* ElementType;

    public uint RID => flags >> 8;
    public uint MetadataToken => RID | 0x2000000;

    public EEClass* Class => (EEClass*)((canonMT & 1) == 0 ? canonMT : ((MethodTable*)(canonMT & ~1))->canonMT);
}