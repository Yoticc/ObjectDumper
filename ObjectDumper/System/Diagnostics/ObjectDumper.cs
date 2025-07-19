#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

namespace System.Diagnostics;
public unsafe static class ObjectDumper
{
    public static void DumpType<T>() => DumpType(typeof(T));

    public static void DumpType(Type type)
    {
        var typeHandle = type.TypeHandle.Value;
        var methodTable = (MethodTable*)typeHandle;
        DumpMethodTable(methodTable);
    }

    static void DumpMethodTable(MethodTable* methodTable)
    {

    }

    public static void DumpObject(object @object)
    {
        using var terminal = new Terminal();
        var serializationContext = new SerializationContext(terminal);

        VerifyObject(@object);

        serializationContext.EndSerialization();
        terminal.Flush();
    }

    static void VerifyObject(object @object)
    {
        const int InitialCapacity = 1 << 8;

        var handles = new List<nint>(InitialCapacity);
        var objects = new List<nint>(InitialCapacity);
        ScanObject(@object);

        Dispose(handles);

        void ScanObject(object @object)
        {
            var handle = HandleManager.AllocateHandle(@object);
            handles.Add(handle);

            var objectPointer = *(nint*)&@object;
            var methodTable = *(MethodTable**)objectPointer;
            var eeClass = methodTable->Class;
            var objectSize = methodTable->BaseSize;
            var actualObjectSize = objectSize - eeClass->ObjectHeaderSize;

            objects.Add(objectPointer);

            var remainsFields = eeClass->NumFields;
            var parentMethodTable = methodTable->ParentMethodTable;

            do
            {
                var firstFieldIndex = 0;
                var fieldCount = eeClass->NumFields;
                if (parentMethodTable is not null)
                {
                    fieldCount -= parentMethodTable->Class->NumFields;
                    firstFieldIndex = remainsFields - fieldCount;
                    remainsFields -= fieldCount;
                }

                var fieldDesc = eeClass->FieldDesc;
                var fieldCounter = 0;
                while (fieldCounter < fieldCount)
                {
                    if (!fieldDesc->IsStatic)
                    {
                        if (fieldDesc->Type is CorElementType.Class)
                        {
                            var offset = fieldDesc->Offset;

                            var root = objectPointer + sizeof(nint) + offset;
                            var fieldObject = *(object*)root;
                            if (fieldObject is not null)
                            {
                                var objectIndex = GetScannedObjectIndex(*(nint*)&fieldObject, objects);
                                var isObjectNotExists = objectIndex == -1;
                                if (isObjectNotExists)
                                    objectIndex = objects.Count;



                                if (isObjectNotExists)
                                    ScanObject(fieldObject);
                            }
                        }

                        fieldCounter++;
                    }

                    fieldDesc++;
                }

                methodTable = parentMethodTable;
                parentMethodTable = methodTable->ParentMethodTable;
                eeClass = methodTable->Class;
            }
            while (methodTable is not null && remainsFields > 0);
        }
        

        static int GetScannedObjectIndex(nint @object, List<nint> objects)
        {
            var count = objects.Count;
            for (var index = count - 1; index >= 0; index--)
                if (objects[index] == @object)
                    return index;

            return -1;
        }

        static void Dispose(List<nint> handles)
        {
            foreach (var handle in handles)
                HandleManager.FreeHandle(handle);
        }
    }

    struct ObjectRoot
    {
        public ObjectRoot(int objectIndex, int objectOffset) => (ObjectIndex, ObjectOffset) = (objectIndex, objectOffset);

        public int ObjectIndex, ObjectOffset;
    }


    struct SerializationContext
    {
        public SerializationContext(Terminal terminal)
        {
            this.terminal = terminal;

            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        Terminal terminal;
        Stopwatch stopwatch;

        public void EndSerialization()
        {
            stopwatch.Stop();
            var timeSpent = $"{Math.Round(stopwatch.Elapsed.TotalMilliseconds, 4)}ms";
            var timestamp = $"{DateTime.Now:dd'/'MM'/'yyyy' 'HH':'mm' 'tt}";

            terminal
            .Style(ConsoleForegroundColor.Gray)
            .Write("======= Verification info =======\n"u8)
            .Write("Timestamp: "u8).Write(timestamp).NewLine()
            .Write("Time spent: "u8).Write(timeSpent).NewLine()
            .ClearStyle();
        }
    }
}