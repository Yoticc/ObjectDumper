#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
unsafe partial class Terminal : IDisposable
{
    public void ResetFormatting() => TerminalWriter.ResetFormatting(ref buffer);

    public void SetStyle(TerminalStyle style) => TerminalWriter.SetStyle(ref buffer, style);

    public void SetForeground(TerminalColor foreground) => TerminalWriter.SetForeground(ref buffer, foreground);

    public void ResetForeground() => TerminalWriter.ResetForeground(ref buffer);

    public void SetBackground(TerminalColor background) => TerminalWriter.SetBackground(ref buffer, background);

    public void ResetBackground() => TerminalWriter.ResetBackground(ref buffer);

    public void Write(char symbol)
    {
        TerminalWriter.Write(ref buffer, symbol);
        EnsureCapacity();
    }

    public void Write(void* array, int length)
    {
        TerminalWriter.Write(ref buffer, array, length);
        EnsureCapacity();
    }

    public void Write(ReadOnlySpan<byte> span)
    {
        TerminalWriter.Write(ref buffer, span);
        EnsureCapacity();
    }

    public void Write(Span<byte> span)
    {
        TerminalWriter.Write(ref buffer, span);
        EnsureCapacity();
    }

    public void WriteString(string text)
    {
        TerminalWriter.WriteString(ref buffer, text);
        EnsureCapacity();
    }

    public void WriteStringLiteral(ReadOnlySpan<char> span)
    {
        TerminalWriter.WriteStringLiteral(ref buffer, span);
        EnsureCapacity();
    }

    public void WriteChars(char* chars, int length)
    {
        TerminalWriter.WriteChars(ref buffer, chars, length);
        EnsureCapacity();
    }

    public void NewLine()
    {
        TerminalWriter.NewLine(ref buffer);
        EnsureCapacity();
    }

    public void WriteUnsignedIntegerWithLeadingZero(ulong value, int digits)
    {
        TerminalWriter.WriteUnsignedIntegerWithLeadingZero(ref buffer, value, digits);
        EnsureCapacity();
    }

    public void WriteUnsignedInteger(ulong value)
    {
        TerminalWriter.WriteUnsignedInteger(ref buffer, value);
        EnsureCapacity();
    }

    public void WriteHexIntegerWithLeadingZero(long value, int digits)
    {
        TerminalWriter.WriteHexIntegerWithLeadingZero(ref buffer, value, digits);
        EnsureCapacity();
    }

    public void WriteHexIntegerWithLeadingZero(ulong value, int digits)
    {
        TerminalWriter.WriteHexIntegerWithLeadingZero(ref buffer, value, digits);
        EnsureCapacity();
    }

    public void WriteHexInteger(ulong value)
    {
        TerminalWriter.WriteHexInteger(ref buffer, value);
        EnsureCapacity();
    }

    public void WriteHexByteArray(byte* array, int length)
    {
        TerminalWriter.WriteHexByteArray(ref buffer, array, length);
        EnsureCapacity();
    }

    public void WritePointer(nint address, char type = '0')
    {
        TerminalWriter.WritePointer(ref buffer, address, type);
        EnsureCapacity();
    }
}