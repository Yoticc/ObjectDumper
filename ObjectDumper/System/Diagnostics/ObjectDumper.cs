using Microsoft.VisualBasic;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
#pragma warning disable CA2265 // Do not compare Span<T> to 'null' or 'default'
namespace System.Diagnostics;
public unsafe static class ObjectDumper
{
    static MethodTable* TheObjectMethodTable = (MethodTable*)typeof(object).TypeHandle.Value;
    static MethodTable* TheStringMethodTable = (MethodTable*)typeof(string).TypeHandle.Value;

    static void PerformDump(Action<Terminal> dumpAction)
    {
        using var terminal = new Terminal();
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        terminal.SetForeground(TerminalColor.Gray);
        terminal.Write("======================= Dump ========================\n\n"u8);
        terminal.ResetForeground();

        dumpAction(terminal);
        stopwatch.Stop();

        terminal.SetForeground(TerminalColor.Gray);
        terminal.Write("Timestamp: "u8);
        terminal.WriteString($"{DateTime.Now:dd'/'MM'/'yyyy' 'HH':'mm' 'tt}\n");
        terminal.Write("Time spent: "u8);
        terminal.WriteString($"{Math.Round(stopwatch.Elapsed.TotalMilliseconds, 4)}ms\n");
        terminal.ResetFormatting();
    }

    public static void DumpType<T>() => DumpType(typeof(T));

    public static void DumpType(Type type) => DumpObject(RuntimeHelpers.GetUninitializedObject(type));
    
    public static void DumpValueType<TStruct>(TStruct value) where TStruct : struct
    {
        var pointer = (nint)(&value);
        var methodTable = (MethodTable*)typeof(TStruct).TypeHandle.Value;
        PerformDump(terminal => InternalDumpValueType(terminal, pointer, methodTable));
    }

    static void InternalDumpValueType(Terminal terminal, nint pointer, MethodTable* methodTable)
    {
        var commentsBuffer = stackalloc byte[512];

        var eeClass = methodTable->Class;
        var name = methodTable->GetName();
        var size = methodTable->BaseSize - eeClass->BaseSizePadding - sizeof(nint);

        terminal.Write("#000  "u8);
        terminal.SetStyle(TerminalStyle.Inverse);
        terminal.WriteString(name);
        terminal.ResetFormatting();
        terminal.NewLine();
        terminal.WritePointer(pointer);
        terminal.Write(" size="u8);
        terminal.WritePointer((nint)size, 'h');
        terminal.NewLine();

        using (var objectSerialization = new ObjectSerializationContext(terminal, commentsBuffer, pointer, methodTable, eeClass))
        {
            objectSerialization.SkipNextMethodTable();

            var fieldCount = eeClass->NumFields;

            FieldDesc* bestField;
            var lastOffset = -1;
            int bestOffset;
            while (true)
            {
                bestOffset = int.MaxValue;
                bestField = null;

                for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                {
                    var field = eeClass->FieldDesc + fieldIndex;
                    var offset = field->Offset;

                    if (offset > lastOffset && bestOffset > offset)
                    {
                        bestOffset = offset;
                        bestField = field;
                    }
                }

                if (bestField is null)
                    break;

                lastOffset = bestField->Offset;

                objectSerialization.AppendField(bestField, isValueType: true);
            }
        }

        terminal.NewLine();
    }

    public static void DumpObject(object @object)
    {
        using var objectCollection = new PinnedObjectCollection();
        objectCollection.AddObject(@object);
        PerformDump(terminal => InternalDumpObject(terminal, objectCollection));
    }

    [SkipLocalsInit]
    static void InternalDumpObject(Terminal terminal, PinnedObjectCollection objects)
    {
        var classNames = new string[64];
        var methodTables = stackalloc MethodTable*[64];
        var commentsBuffer = stackalloc byte[512];

        for (var objectIndex = 0; objectIndex < objects.Count; objectIndex++)
        {
            var pobject = objects[objectIndex];
            var methodTable = *(MethodTable**)pobject;
            var eeClass = methodTable->Class;
            var actualObjectSize = methodTable->BaseSize;
            var objectSize = actualObjectSize - eeClass->BaseSizePadding;

            terminal.Write('#');
            terminal.WriteUnsignedIntegerWithLeadingZero((uint)objectIndex, 3);
            terminal.Write("  "u8);

            var methodTablesCount = 0;
            do
            {
                methodTables[methodTablesCount] = methodTable;
                classNames[methodTablesCount] = methodTable->GetName();
                methodTablesCount++;
            }
            while ((methodTable = methodTable->ParentMethodTable) is not null);

            for (var methodTableIndex = 0; methodTableIndex < methodTablesCount; methodTableIndex++)
            {
                if ((methodTablesCount - 1 - methodTableIndex) % 2 == 0)
                    terminal.SetStyle(TerminalStyle.Inverse);
                else terminal.SetBackground(TerminalColor.Black);

                terminal.WriteString(classNames[methodTableIndex]);
                terminal.ResetFormatting();

                if (methodTableIndex != methodTablesCount - 1)
                {
                    terminal.SetForeground(TerminalColor.Gray);
                    terminal.Write(" : "u8);
                    terminal.ResetFormatting();
                }
            }

            methodTable = *methodTables;

            terminal.NewLine();
            terminal.WritePointer(pobject);
            terminal.Write(" size="u8);
            terminal.WritePointer((nint)objectSize, 'h');

            if (objectSize != actualObjectSize)
            {
                terminal.Write(" actualSize="u8);
                terminal.WritePointer((nint)actualObjectSize, 'h');
            }

            terminal.NewLine();

            using (var objectSerialization = new ObjectSerializationContext(terminal, commentsBuffer, pobject, methodTable, eeClass))            
            {
                var methodTableIndex = methodTablesCount - 1;
                methodTable = methodTables[methodTableIndex];
                if (methodTable == TheObjectMethodTable)
                {
                    methodTableIndex--;
                    objectSerialization.NotifyObjectMethodTable();
                }

                var pastTotalFieldsCount = 0;
                for (; methodTableIndex >= 0; methodTableIndex--)
                {
                    methodTable = methodTables[methodTableIndex];
                    eeClass = methodTable->Class;

                    var totalFieldCount = eeClass->NumFields;
                    var fieldCount = totalFieldCount - pastTotalFieldsCount;
                    if (fieldCount == 0)
                    {
                        objectSerialization.SkipNextMethodTable();
                        continue;
                    }

                    var name = classNames[methodTableIndex];
                    objectSerialization.NotifyNextMethodTable(name);

                    var firstField = eeClass->FieldDesc;
                    for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                    {
                        var field = eeClass->FieldDesc + fieldIndex;
                        var offset = field->Offset;

                        if (firstField->Offset > offset)
                            firstField = field;                   
                    }

                    FieldDesc* bestField;
                    var lastOffset = -1;
                    int bestOffset;
                    while (true)
                    {
                        bestOffset = int.MaxValue;
                        bestField = null;

                        for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                        {
                            var field = eeClass->FieldDesc + fieldIndex;
                            var offset = field->Offset;

                            if (offset > lastOffset && bestOffset > offset)
                            {
                                bestOffset = offset;
                                bestField = field;
                            }
                        }

                        if (bestField is null)
                            break;

                        lastOffset = bestField->Offset;

                        objectSerialization.AppendField(bestField, isValueType: false);

                        if (bestField->Type == CorElementType.Class)
                        {
                            var @object = *(object*)(pobject + bestField->GetOffsetForObject());

                            if (@object is not null)
                                objects.AddObject(@object);
                        }
                    }

                    pastTotalFieldsCount = totalFieldCount;
                }
            }

            methodTable = *methodTables;
            if (methodTable == TheStringMethodTable)
            {
                var @string = *(string*)&pobject;
                @string = $"\"{@string}\"";

                fixed (char* chars = @string)
                {
                    var length = @string.Length;
                    var remindLength = length;
                    while (remindLength > 0)
                    {
                        var bytesToWrite = Math.Min(remindLength, 53);
                        terminal.WriteChars(chars + (length - remindLength), bytesToWrite);
                        remindLength -= bytesToWrite;

                        terminal.NewLine();
                    }
                }
            }

            terminal.NewLine();
        }
    }

    struct ObjectSerializationContext : IDisposable
    {
        public ObjectSerializationContext(Terminal terminal, byte* commentBuffer, nint pobject, MethodTable* methodTable, EEClass* eeClass)
        {
            this.pobject = (byte*)pobject;
            this.methodTable = methodTable;
            this.eeClass = eeClass;
            this.terminal = terminal;

            comment = new Comment(terminal, commentBuffer);
        }

        Terminal terminal;
        Comment comment;
        byte* pobject;
        MethodTable* methodTable;
        EEClass* eeClass;
        int bodyPosition;
        int methodTableOrdinal = -1;
        State state = State.EmptyLine;

        int bytesInLine => bodyPosition < 0 ? (16 - -bodyPosition) & 15 : bodyPosition & 15;
        int bytesIsAvailable => 16 - bytesInLine;

        public void NotifyObjectMethodTable()
        {
            methodTableOrdinal++;

            var expectedObjectHeaderSize = 4/*reserved for gc*/ + 4/*sync block or hashcode*/ + sizeof(nint)/*pmt*/;
            if (eeClass->BaseSizePadding == expectedObjectHeaderSize)
            {
                bodyPosition = -16;
                var gcUnused = *(int*)&pobject[-8];
                var syncblock = *(int*)&pobject[-4];

                AppendObjectTitle("Object");
                AppendBlankBytes(8);

                AppendBytes(4, BytesDataType.Padding, $"gc_unused({gcUnused})");
                AppendBytes(4, BytesDataType.Value, $"syncblock({syncblock})");
                AppendBytes(8, BytesDataType.Value, $"MethodTable({(nint)methodTable:X}h)");
            }
            else
            {
                AppendObjectTitle("Object");
                AppendBytes(8, BytesDataType.Value, $"MethodTable({(nint)methodTable:X}h)");
            }
        }

        public void NotifyNextMethodTable(string objectName)
        {
            methodTableOrdinal++;
            AppendObjectTitle(objectName);
        }

        public void SkipNextMethodTable() => methodTableOrdinal++;

        void EnsureState()
        {
            if (state == State.EmptyLine)
            {
                WriteAddressLabel();
                state = State.HasLabel;
            }
            else if (state == State.FullLine)
            {
                comment.Push();
                terminal.NewLine();
                WriteAddressLabel();
                state = State.HasLabel;
            }
        }

        void AppendObjectTitle(string objectTitle)
        {
            EnsureState();
            comment.AppendObjectTitle(objectTitle);
        }

        void AppendComment(string commentText, BytesDataType type, int ordinal)
        {
            EnsureState();
            comment.AppendDescription(commentText, type, ordinal);
        }

        public void AppendField(FieldDesc* field, bool isValueType)
        {
            var size = field->GetSize();
            var offset = field->GetOffset(isValueType);
            var objectOffset = field->GetOffsetForObject();
            var pointer = (void*)(pobject + objectOffset);
            var comment = this.comment.GetFieldDescription(field, pointer, size);

            CheckPaddingsAndAppendBytes(offset, size, BytesDataType.Value, comment);
        }

        void EnsurePadding(int position)
        {
            var padding = position - bodyPosition;
            if (padding > 0)
                AppendBytes(padding, BytesDataType.Padding, $"padding[{padding}]");
        }

        void CheckPaddingsAndAppendBytes(int position, int length, BytesDataType type, string comment)
        {
            EnsurePadding(position);
            AppendBytes(length, type, comment);
        }

        void AppendBytes(int length, BytesDataType type, string comment)
        {
            AppendComment(comment, type, methodTableOrdinal);

            while (length > 0)
            {
                EnsureState();
                if (state != State.HasLabel)
                    terminal.Write(' ');

                var availableBytes = bytesIsAvailable;
                var isAllBytes = length >= availableBytes;
                var bytesToWrite = isAllBytes ? availableBytes : length;

                terminal.SetForeground((TerminalColor)type);

                if (methodTableOrdinal % 2 == 0)
                    terminal.SetStyle(TerminalStyle.Inverse);
                else terminal.SetBackground(TerminalColor.Black);

                terminal.WriteHexByteArray(pobject + bodyPosition, bytesToWrite);
                bodyPosition += bytesToWrite;

                terminal.ResetFormatting();

                length -= bytesToWrite;
                state = isAllBytes ? State.FullLine : State.NotEmpty;
            }
        }

        public void AppendBlankBytes(int length)
        {
            while (length > 0)
            {
                EnsureState();
                if (state != State.HasLabel)
                    terminal.Write(' ');

                var availableBytes = bytesIsAvailable;
                var isAllBytes = length >= availableBytes;
                var bytesToWrite = isAllBytes ? availableBytes : length;
                var blanksToWrite = bytesToWrite * 3 - 1;

                terminal.WriteBlanks(blanksToWrite);
                bodyPosition += bytesToWrite;

                length -= bytesToWrite;
                state = isAllBytes ? State.FullLine : State.NotEmpty;
            }
        }

        void WriteAddressLabel()
        {
            terminal.SetForeground(TerminalColor.Gray);
            terminal.WriteHexIntegerWithLeadingZero(bodyPosition, 4);
            terminal.Write("  "u8);
            terminal.ResetFormatting();
        }

        public void Dispose()
        {
            var size = (int)methodTable->BaseSize - eeClass->BaseSizePadding;
            if (!eeClass->IsValueType)
                size += sizeof(nint);

            EnsurePadding(size);

            if (comment.HasContent())
            {
                if (state != State.FullLine)
                    AppendBlankBytes(bytesIsAvailable);
                comment.PushNoChecks();
            }

            terminal.NewLine();
        }

        struct Comment
        {
            public Comment(Terminal terminal, byte* buffer)
            {
                this.terminal = terminal;
                this.buffer = initialBuffer = buffer;
            }

            Terminal terminal;
            byte* initialBuffer;
            public byte* buffer;
            int length => (int)(buffer - initialBuffer);

            public bool HasContent() => length != 0;

            public void Push()
            {
                if (HasContent())
                    PushNoChecks();
            }

            public void PushNoChecks()
            {
                terminal.Write("  "u8);
                terminal.Write(initialBuffer, length);

                buffer = initialBuffer;
            }

            public string GetFieldDescription(FieldDesc* field, void* pvalue, int size)
            {
                var pmd = field->DefinedType;
                var definedTypeHandle = RuntimeTypeHandle.FromIntPtr((nint)pmd);
                var module = definedTypeHandle.GetModuleHandle();
                var managedFieldHandle = module.ResolveFieldHandle(field->Token);
                var managedField = FieldInfo.GetFieldFromHandle(managedFieldHandle);
                var managedType = managedField.FieldType;

                var name = managedField.Name;
                var value = field->Type switch
                {
                    CorElementType.I1 => (*(sbyte*)pvalue).ToString(),
                    CorElementType.U1 => (*(byte*)pvalue).ToString(),
                    CorElementType.I2 => (*(short*)pvalue).ToString(),
                    CorElementType.U2 => (*(ushort*)pvalue).ToString(),
                    CorElementType.Char => $"\'{*(char*)pvalue}\'",
                    CorElementType.I4 => (*(int*)pvalue).ToString(),
                    CorElementType.U4 => (*(uint*)pvalue).ToString(),
                    CorElementType.R4 => (*(float*)pvalue).ToString(),
                    CorElementType.I8 => (*(long*)pvalue).ToString(),
                    CorElementType.U8 => (*(ulong*)pvalue).ToString(),
                    CorElementType.R8 => (*(double*)pvalue).ToString(),
                    CorElementType.Class => GetValueForPointerType(),
                    CorElementType.Object => GetValueForPointerType(),
                    CorElementType.Pointer => GetValueForPointerType(),
                    CorElementType.ValueType => managedType.Name,
                    _ => throw new NotImplementedException()
                };

                return $"{name}({value})";

                string GetValueForPointerType()
                {
                    var pointer = *(nint*)pvalue;
                    if (pointer == default)
                        return "null";

                    return $"0x{*(nint*)pvalue:X}";
                }
            }

            public void AppendDescription(string description, BytesDataType type, int ordinal)
            {
                PrepareFormatting(type, ordinal);
                TerminalWriter.WriteString(ref buffer, description);
                TerminalWriter.ResetFormatting(ref buffer);
                TerminalWriter.Write(ref buffer, ' ');
            }

            public void AppendObjectTitle(string objectTitle)
            {
                TerminalWriter.SetForeground(ref buffer, TerminalColor.White);
                TerminalWriter.WriteString(ref buffer, objectTitle);
                TerminalWriter.SetForeground(ref buffer, TerminalColor.Gray);
                TerminalWriter.Write(ref buffer, ": "u8);
                TerminalWriter.ResetFormatting(ref buffer);
            }

            void PrepareFormatting(BytesDataType type, int ordinal)
            {
                TerminalWriter.SetForeground(ref buffer, (TerminalColor)type);

                if (ordinal % 2 == 0)
                    TerminalWriter.SetStyle(ref buffer, TerminalStyle.Inverse);
                else TerminalWriter.SetBackground(ref buffer, TerminalColor.Black);
            }
        }

        enum State
        {
            EmptyLine,
            HasLabel,
            NotEmpty,
            FullLine
        }
    }

    enum BytesDataType
    {
        Padding = TerminalColor.Gray,
        Value = TerminalColor.White
    }
}