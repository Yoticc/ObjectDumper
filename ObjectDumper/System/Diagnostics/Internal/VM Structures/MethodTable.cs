using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
unsafe struct MethodTable
{
    [FieldOffset(0x00)] public ushort ComponentSize;
    [FieldOffset(0x00)] uint flags;
    [FieldOffset(0x04)] public uint BaseSize;
    [FieldOffset(0x08)] uint flags2;
    [FieldOffset(0x10)] public MethodTable* ParentMethodTable;
    [FieldOffset(0x18)] public void* Module;
    [FieldOffset(0x28)] nint canonMT;
    [FieldOffset(0x30)] public unsafe void* ElementType;

    public uint RID => flags2 >> 8;
    public uint Token => RID | 0x2000000;

    public EEClass* Class => (EEClass*)((canonMT & 1) == 0 ? canonMT : ((MethodTable*)(canonMT & ~1))->canonMT);

    public string GetName()
    {
        var self = Unsafe.AsPointer(ref this);
        var managedType = Type.GetTypeFromHandle(RuntimeTypeHandle.FromIntPtr((nint)self));
        if (managedType is not null)
            return managedType.Name;
        else return "<<EMPTY_NAME>>";
    }
}