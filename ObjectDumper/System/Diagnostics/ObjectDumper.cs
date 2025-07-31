using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
#pragma warning disable CA2265 // Do not compare Span<T> to 'null' or 'default'
namespace System.Diagnostics;
public unsafe static class ObjectDumper
{
    static ObjectDumper() => TheObjectMethodTable = (MethodTable*)typeof(object).TypeHandle.Value;

    static MethodTable* TheObjectMethodTable;

    static void PerformDump(Action<Terminal> dumpAction)
    {
        using var terminal = new Terminal();
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        terminal.SetForeground(TerminalColor.Gray);
        terminal.Write("======================= Dump =======================\n\n"u8);
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

    public static void DumpType(Type type)
    {
        var typeHandle = type.TypeHandle.Value;
        var methodTable = (MethodTable*)typeHandle;
        PerformDump(terminal => InternalDumpType(terminal, methodTable));
    }

    public static void DumpObject(object @object)
    {
        using var objectCollection = new PinnedObjectCollection();
        objectCollection.AddObject(@object);
        PerformDump(terminal => InternalDumpObject(terminal, objectCollection));
    }

    static void InternalDumpType(Terminal terminal, MethodTable* methodTable)
    {
        
    }

    [SkipLocalsInit]
    static void InternalDumpObject(Terminal terminal, PinnedObjectCollection objects)
    {
        var classNames = new string[256];
        var methodTables = stackalloc MethodTable*[256];
        var commentsBuffer = stackalloc byte[512];

        for (var objectIndex = 0; objectIndex < objects.Count; objectIndex++)
        {
            var pobject = objects[objectIndex];
            var methodTable = *(MethodTable**)pobject;
            var eeClass = methodTable->Class;
            var actualObjectSize = methodTable->BaseSize;
            var objectSize = actualObjectSize - eeClass->ObjectHeaderAndGCHeaderSize;

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
                if ((methodTablesCount - methodTableIndex) % 2 == 0)
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
            terminal.Write(" actualSize="u8);
            terminal.WritePointer((nint)actualObjectSize, 'h');

            terminal.NewLine();
            terminal.SetForeground(TerminalColor.Gray);
            terminal.Write("      0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F"u8);

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

                    for (var fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                    {
                        var field = eeClass->FieldDesc + fieldIndex;
                        objectSerialization.AppendField(field);

                        if (field->Type == CorElementType.Class)
                        {
                            var @object = *(object*)(pobject + field->GetOffsetForObject());
                            objects.TryAddObject(@object);
                        }
                    }

                    pastTotalFieldsCount = totalFieldCount;
                }
            }
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

            if (eeClass->IsValueType)
                pobject += sizeof(nint);
        }

        Terminal terminal;
        Comment comment;
        byte* pobject;
        MethodTable* methodTable;
        EEClass* eeClass;
        int bodyPosition;
        int methodTableOrdinal = -1;
        bool hasRecord;

        public void NotifyObjectMethodTable()
        {
            methodTableOrdinal++;

            if (eeClass->ObjectHeaderAndGCHeaderSize == 4/*reserved for gc*/ + 4/*sync block*/ + sizeof(nint)/*pmt*/)
            {
                bodyPosition = -16;
                var gcUnused = *(int*)&pobject[-8];
                var syncblock = *(int*)&pobject[-4];

                comment.AppendObjectTitle("Object");
                AppendBlankBytes(8);

                InternalAppendBytes(4, ByteDataType.Padding, $"gc_unused({gcUnused})");
                InternalAppendBytes(4, ByteDataType.Value, $"syncblock({syncblock})");
                InternalAppendBytes(8, ByteDataType.Value, $"MethodTable({(nint)methodTable:X}h)");
            }
            else
            {
                comment.AppendObjectTitle("Object");
                InternalAppendBytes(8, ByteDataType.Value, $"MethodTable({(nint)methodTable:X}h)");
            }
        }

        public void NotifyNextMethodTable(string objectName)
        {
            methodTableOrdinal++;
            comment.AppendObjectTitle(objectName);
        }

        public void SkipNextMethodTable() => methodTableOrdinal++;

        void PrepareFormatting(ByteDataType type)
        {
            terminal.SetForeground(type.GetTerminalForegroundColor());

            if (methodTableOrdinal % 2 == 0)
                terminal.SetStyle(TerminalStyle.Inverse);
            else terminal.SetBackground(TerminalColor.Black);
        }

        public void AppendField(FieldDesc* field)
        {
            var size = field->GetSize();
            var offset = field->GetOffset();
            var objectOffset = field->GetOffsetForObject();
            var pointer = (void*)(pobject + objectOffset);
            var comment = this.comment.GetFieldDescription(field, pointer, size);

            AppendBytes(offset, size, ByteDataType.Value, comment);
        }
               
        void AppendBytes(int position, int length, ByteDataType type, string comment)
        {
            var padding = position - bodyPosition;
            if (padding > 0)
                InternalAppendBytes(padding, ByteDataType.Padding, $"padding[{padding}]");

            InternalAppendBytes(length, type, comment);
        }

        int GetAvailableBytes()
        {
            var inLineBytes = bodyPosition < 0 ? Math.Abs(bodyPosition % 16) : bodyPosition & 15;
            
            if (inLineBytes == 0)
            {
                if (hasRecord)
                    comment.Push();

                terminal.Write('\n');
                WriteAddressLabel();
            }
            else terminal.Write(' ');

            if (!hasRecord)
                hasRecord = true;

            return 16 - inLineBytes;
        }

        void InternalAppendBytes(int length, ByteDataType type, string comment)
        {
            this.comment.AppendComment(comment, type, methodTableOrdinal);

            while (length > 0)
            {
                var availableBytes = GetAvailableBytes();
                var bytesToWrite = length <= availableBytes ? length : availableBytes;

                PrepareFormatting(type);

                terminal.WriteHexByteArray(pobject + bodyPosition, bytesToWrite);
                bodyPosition += bytesToWrite;

                terminal.ResetFormatting();

                length -= bytesToWrite;
            }
        }

        public void AppendBlankBytes(int length)
        {
            while (length > 0)
            {
                var availableBytes = GetAvailableBytes();
                var bytesToWrite = length <= availableBytes ? length : availableBytes;
                var blanksToWrite = bytesToWrite * 3 - 1;
                terminal.WriteBlanks(blanksToWrite);
                bodyPosition += bytesToWrite;

                length -= bytesToWrite;
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
            if (comment.Length > 0)
            {
                var inLineBytes = bodyPosition < 0 ? Math.Abs(bodyPosition % 16) : bodyPosition & 15;
                var bytesToWrite = 16 - inLineBytes;

                AppendBlankBytes(bytesToWrite);

                comment.Push();
            }

            terminal.Write("\n\n"u8);
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

            public int Length => (int)(buffer - initialBuffer);

            public void Push()
            {
                var length = Length;
                if (length > 0)
                {
                    terminal.Write("  "u8);
                    terminal.Write(initialBuffer, length);

                    buffer = initialBuffer;
                }
            }

            public string GetFieldDescription(FieldDesc* field, void* pointer, int size)
            {
                var pmd = field->DefinedType;
                var definedTypeHandle = RuntimeTypeHandle.FromIntPtr((nint)pmd);
                var module = definedTypeHandle.GetModuleHandle();
                var managedFieldHandle = module.ResolveFieldHandle(field->Token);
                var managedField = FieldInfo.GetFieldFromHandle(managedFieldHandle);

                var name = managedField.Name;
                string value = field->Type switch
                {
                    CorElementType.I1 => (*(sbyte*)pointer).ToString(),
                    CorElementType.U1 => (*(byte*)pointer).ToString(),
                    CorElementType.I2 => (*(short*)pointer).ToString(),
                    CorElementType.U2 => (*(ushort*)pointer).ToString(),
                    CorElementType.Char => (*(char*)pointer).ToString(),
                    CorElementType.I4 => (*(int*)pointer).ToString(),
                    CorElementType.U4 => (*(uint*)pointer).ToString(),
                    CorElementType.R4 => (*(float*)pointer).ToString(),
                    CorElementType.I8 => (*(long*)pointer).ToString(),
                    CorElementType.U8 => (*(ulong*)pointer).ToString(),
                    CorElementType.R8 => (*(double*)pointer).ToString(),
                    CorElementType.Class => $"0x{*(nint*)pointer:X}",
                    CorElementType.Object => $"0x{*(nint*)pointer:X}",
                    CorElementType.Pointer => $"0x{*(nint*)pointer:X}",
                    CorElementType.ValueType => Type.GetTypeFromHandle(definedTypeHandle).Name,
                    _ => throw new NotImplementedException()
                };

                return $"{name}({value})";
            }

            public void AppendComment(string comment, ByteDataType type, int ordinal)
            {
                PrepareFormatting(type, ordinal);
                TerminalWriter.WriteString(ref buffer, comment);
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

            void PrepareFormatting(ByteDataType type, int ordinal)
            {
                TerminalWriter.SetForeground(ref buffer, type.GetTerminalForegroundColor());

                if (ordinal % 2 == 0)
                    TerminalWriter.SetStyle(ref buffer, TerminalStyle.Inverse);
                else TerminalWriter.SetBackground(ref buffer, TerminalColor.Black);
            }
        }

        enum LineState
        {
            NotStarted,
            EmptyLine,
            NotEmptyLine,
            FullLine
        }
    }

    internal enum ByteDataType
    {
        Padding,
        Value
    }
}

static class ByteDataTypeExtensions
{
    public static TerminalColor GetTerminalForegroundColor(this ObjectDumper.ByteDataType type)
        => type switch
        {
            ObjectDumper.ByteDataType.Value => TerminalColor.White,
            ObjectDumper.ByteDataType.Padding => TerminalColor.Gray,
            _ => throw new NotImplementedException()
        };
}