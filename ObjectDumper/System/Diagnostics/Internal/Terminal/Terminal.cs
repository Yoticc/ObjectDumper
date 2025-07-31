using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
unsafe partial class Terminal : IDisposable
{
    const int BufferCapacity = 1 << 15;

    public Terminal()
    {
        consoleHandle = GetConsoleOutHandle();
        PreserveConsoleOptions();
        SetupConsoleOptions();
        AllocateBuffer();
    }

    nint consoleHandle;

    uint previousConsoleMode;
    [AllowNull] Encoding previousConsoleEncoding;

    byte* initialBuffer;
    byte* buffer;
    int bufferLength => (int)(buffer - initialBuffer);

    void EnsureCapacity()
    {
        const int LengthThreshold = BufferCapacity - 256;

        if ((uint)bufferLength > LengthThreshold)
            FlushNoChecks();
    }

    public void Flush()
    {
        if (bufferLength == 0)
            return;

        FlushNoChecks();
    }

    void FlushNoChecks()
    {
        WriteConsoleA(consoleHandle, initialBuffer, bufferLength, null, null);
        buffer = initialBuffer;
    }

    void AllocateBuffer() => buffer = initialBuffer = (byte*)Marshal.AllocCoTaskMem(BufferCapacity);

    void FreeBuffer() => Marshal.FreeCoTaskMem((nint)initialBuffer);

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

    public void Dispose()
    {
        Flush();
        FreeBuffer();
        RestoreConsoleOptions();
    }

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