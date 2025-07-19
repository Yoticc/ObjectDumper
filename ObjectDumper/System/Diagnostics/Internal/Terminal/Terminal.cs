using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

unsafe partial class Terminal : IDisposable
{
    public Terminal()
    {
        consoleHandle = GetConsoleOutHandle();
        PreserveConsoleOptions();
        SetupConsoleOptions();

        buffer = new Utf8StringBuilder();
    }

    nint consoleHandle;

    uint previousConsoleMode;
    [AllowNull] Encoding previousConsoleEncoding;

    Utf8StringBuilder buffer;

    public Terminal Write(char symbol) => Write((byte)symbol);

    public Terminal Write(byte symbol)
    {
        buffer.Append(symbol);
        return this;
    }

    public Terminal Write(Span<byte> text)
    {
        buffer.Append(text);
        return this;
    }

    public Terminal Write(ReadOnlySpan<byte> text)
    {
        buffer.Append(text);
        return this;
    }

    public Terminal Write(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        fixed (byte* pointer = bytes)
        {
            var span = new ReadOnlySpan<byte>(pointer, bytes.Length);
            return Write(span);
        }
    }

    public Terminal WriteInteger(long value)
    {
        var stack = stackalloc byte[16];
        var span = FormatInteger(stack, value);
        Write(span);

        return this;
    }

    Span<byte> FormatInteger(byte* input, long value)
    {
        const int MaxLength = 8;

        var pointer = input + MaxLength;
        do
        {
            *--pointer = (byte)(value % 10 + (byte)'0');
            value /= 10;
        } while (value != 0);

        var left = (int)(pointer - input);
        input += left;
        var length = MaxLength - left;
        var span = new Span<byte>(input, length);

        return span;
    }

    public Terminal ClearStyle() => Write("\e[0m");

    public Terminal Style(ConsoleTextStyles styles)
    {
        Write("\e[0"u8);

        var stylesValue = styles.Value;
        long value;

        var stack = stackalloc byte[32];
        for (var shift = 0; shift < 64; shift += 8)
            if ((value = stylesValue >> shift & 0xFF) != 0)
            {
                Write(';');
                Write(FormatInteger(stack, value));
            }

        Write('m');

        return this;
    }

    public Terminal NewLine() => Write("\n"u8);

    public void Flush()
    {
        var iterator = buffer.GetIterator();

        while (iterator.Next(out var span))
            fixed (byte* buffer = span)
                WriteConsoleA(consoleHandle, buffer, span.Length, null, null);

        buffer.Dispose();
        buffer = new Utf8StringBuilder();
    }

    void SetupConsoleOptions()
    {
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        ConsoleMode = previousConsoleMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        Console.OutputEncoding = Encoding.ASCII;
    }

    void PreserveConsoleOptions()
    {
        previousConsoleMode = ConsoleMode;
        previousConsoleEncoding = Console.OutputEncoding;
    }

    void RestoreConsoleOptions()
    {
        ConsoleMode = previousConsoleMode;
        Console.OutputEncoding = previousConsoleEncoding;
    }

    uint ConsoleMode { get => GetConsoleMode(consoleHandle); set => SetConsoleMode(consoleHandle, value); }

    public void Dispose() => RestoreConsoleOptions();
    
    const string kernel = "kernel32";

    [LibraryImport(kernel)] 
    internal static partial nint WriteConsoleA(nint handle, byte* buffer, int bytesToWrite, int* bytesWritten, void* reserved);

    static nint GetConsoleOutHandle()
    {
        const int STD_OUTPUT_HANDLE = -11;

        return GetStdHandle(STD_OUTPUT_HANDLE);
    }

    [LibraryImport(kernel)]
    internal static partial nint GetStdHandle(int stdHandle);

    static uint GetConsoleMode(nint consoleHandle)
    {
        GetConsoleMode(consoleHandle, out var mode);
        return mode;
    }

    [LibraryImport(kernel)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetConsoleMode(nint consoleHandle, out uint mode);

    [LibraryImport(kernel)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetConsoleMode(nint consoleHandle, uint mode);
}