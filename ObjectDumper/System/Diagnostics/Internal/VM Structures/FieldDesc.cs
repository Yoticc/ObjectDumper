using System.Reflection;

unsafe struct FieldDesc
{
    MethodTable* methodTableOfEnclosingClass;
    int dword1;
    int dword2;

    public MethodTable* DefinedType => methodTableOfEnclosingClass;

    public int RID => dword1 & (1 << 24) - 1;
    public bool IsStatic => (dword1 & 1 << 24) > 0;
    public bool IsThreadLocal => (dword1 & 1 << 25) > 0;
    public bool IsRVA => (dword1 & 1 << 26) > 0;
    public int Protection => (dword2 >> 3) & (1 << 3) - 1;

    public int Offset => dword2 & (1 << 21) - 1;
    public CorElementType Type => (CorElementType)((dword2 >> 27) & (1 << 5) - 1);

    public int Token => RID | 0x4000000;
}

static unsafe class FieldDescExtensions
{
    public static int GetOffset(this FieldDesc self) => self.Type == CorElementType.ValueType ? self.GetOffsetForValueType() : self.GetOffsetForObject();

    public static int GetOffsetForObject(this FieldDesc self) => self.Offset + sizeof(nint);

    public static int GetOffsetForValueType(this FieldDesc self) => self.Offset;

    public static int GetSize(this FieldDesc self)
    {
        int size;

        var fieldType = self.Type;

        if (fieldType is CorElementType.ValueType)
        {
            // we obtain the handle module through managed components because it does not have a stable native implementation or good interface for obtaining it
            var pmd = self.DefinedType;
            var definedTypeHandle = RuntimeTypeHandle.FromIntPtr((nint)pmd);
            var module = definedTypeHandle.GetModuleHandle();

            var managedFieldHandle = module.ResolveFieldHandle(self.Token);
            var managedField = FieldInfo.GetFieldFromHandle(managedFieldHandle);
            var managedFieldType = managedField.FieldType;
            var managedFieldTypeHandle = managedFieldType.TypeHandle;
            var methodTable = (MethodTable*)managedFieldTypeHandle.Value;
            var eeClass = methodTable->Class;

            size = (int)methodTable->BaseSize - eeClass->ObjectHeaderAndGCHeaderSize;
        }
        else
        {
            size = fieldType switch
            {
                CorElementType.I1 => 1,
                CorElementType.U1 => 1,
                CorElementType.I2 => 2,
                CorElementType.U2 => 2,
                CorElementType.Char => 2,
                CorElementType.I4 => 4,
                CorElementType.U4 => 4,
                CorElementType.R4 => 4,
                CorElementType.I8 => 8,
                CorElementType.U8 => 8,
                CorElementType.R8 => 8,
                CorElementType.Class => sizeof(void*),
                CorElementType.Object => sizeof(void*),
                CorElementType.Pointer => sizeof(void*),
                _ => throw new NotImplementedException()
            };
        }

        return size;
    }
}
