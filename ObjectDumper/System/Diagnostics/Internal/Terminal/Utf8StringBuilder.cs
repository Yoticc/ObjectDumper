using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

unsafe class Utf8StringBuilder : IDisposable
{
    public Utf8StringBuilder() => AllocateInitialMemoryChunk();

    MemoryChunk* rootChunk;
    MemoryChunk* currentChunk;

    public void Append(byte symbol) => Append(new Span<byte>(&symbol, sizeof(byte)));

    public void Append(Span<byte> text) => Append((ReadOnlySpan<byte>)text);

    public void Append(ReadOnlySpan<byte> text)
    {
        var memoryLeft = currentChunk->Left;
        if (text.Length > memoryLeft)
        {
            var newText = text.Slice(0, memoryLeft);
            InternalAppend(newText);

            AllocateNextMemoryChunk();
            text = text.Slice(memoryLeft);
            Append(text);
            return;
        }

        InternalAppend(text);
    }

    void InternalAppend(ReadOnlySpan<byte> text)
    {
        var buffer = new Span<byte>(currentChunk->Cursor, int.MaxValue);
        text.CopyTo(buffer);
        currentChunk->Length += text.Length;
    }

    public SpanIterator GetIterator() => new SpanIterator(this);

    void AllocateInitialMemoryChunk() => currentChunk = rootChunk = MemoryChunk.Allocate();

    void AllocateNextMemoryChunk()
    {
        var previousChunk = currentChunk;
        currentChunk = MemoryChunk.Allocate();

        previousChunk->NextChunk = currentChunk;
    }

    public void Dispose() => rootChunk->Free();

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct MemoryChunk
    {
        const int BlockSize = 1 << 15;

        public MemoryChunk* NextChunk;
        public int Length;
        public fixed byte Buffer[1];

        public int Capacity => BlockSize;
        public int Left => Capacity - Length;
        public byte* Cursor => ((MemoryChunk*)Unsafe.AsPointer(ref this))->Buffer + Length;

        public void Free()
        {
            var next = (MemoryChunk*)Unsafe.AsPointer(ref this);
            while (next is not null)
            {
                var next2 = next->NextChunk;
                Marshal.FreeCoTaskMem((nint)next);
                next = next2;
            }
        }

        public static MemoryChunk* Allocate()
        {
            var size = sizeof(MemoryChunk*) + sizeof(int) + BlockSize;
            var block = (MemoryChunk*)Marshal.AllocCoTaskMem(size);

            block->NextChunk = null;
            block->Length = 0;                

            return block;
        }
    }

    public class SpanIterator
    {
        public SpanIterator(Utf8StringBuilder sb) => chunk = sb.rootChunk;

        MemoryChunk* chunk;

        public bool Next(out Span<byte> span)
        {
            if (chunk is null)
            {
                span = default;
                return false;
            }
            
            span = new Span<byte>(chunk->Buffer, chunk->Length);
            chunk = chunk->NextChunk;

            return true;
        }
    }
}