unsafe struct EEClass
{
    void* guidInfo;
    void* optionalFields;
    MethodTable* methodTable;
    FieldDesc* fieldDesc;
    void* chunks;
    void* objectHandleDelegateOrComInterfaceType;
    void* pccwTemplate;
    int attrClass;
    int vmFlags;
    byte normType;
    byte baseSizePadding;
    short numInstanceFields;
    short numMethods;
    short numStaticFields;
    short numHandleStatics;
    short numThreadStaticFields;
    short numHandleThreadStatics;
    short numNonVirtualSlots;
    int nonGCStaticFieldBytes;
    int nonGCThreadStaticFieldBytes;

    public MethodTable* MethodTable => methodTable;
    public FieldDesc* FieldDesc => fieldDesc;
    public CorElementType CorElementType => (CorElementType)normType;
    public int NumFields => numInstanceFields;
    public int NumStaticFields => numStaticFields;
    public int BaseSizePadding => baseSizePadding;

    public bool IsArray => CorElementType is CorElementType.Array or CorElementType.SZArray;
    public bool IsValueType => CorElementType is CorElementType.ValueType;
}
