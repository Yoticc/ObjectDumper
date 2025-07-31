#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
using System.Numerics;

unsafe static class TerminalWriter
{
    #region Formatting
    public static void ResetFormatting(ref byte* buffer) => SetStyle(ref buffer, TerminalStyle.Reset);

    public static void SetStyle(ref byte* buffer, TerminalStyle style) => SetStyle(ref buffer, (int)style);

    static void SetStyle(ref byte* buffer, int style)
    {
        *(int*)buffer = '\e' | '[' << 8 | '0' + style << 16 | 'm' << 24;
        buffer += 4;
    }

    public static void SetForeground(ref byte* buffer, TerminalColor foreground) => WriteTag(ref buffer, (uint)foreground);

    public static void ResetForeground(ref byte* buffer) => WriteTag(ref buffer, 39); 

    public static void SetBackground(ref byte* buffer, TerminalColor background) => WriteTag(ref buffer, (uint)background + 10);

    public static void ResetBackground(ref byte* buffer) => WriteTag(ref buffer, 49);

    static void WriteTag(ref byte* buffer, uint tag)
    {
        long value = 'm';
        var chars = 3;

        do
        {
            chars++;
            (tag, var reminder) = Math.DivRem(tag, 10);
            value = (value << 8) | '0' + reminder;
        }
        while (tag > 0);

        *(long*)buffer = (value << 8 | '[') << 8 | '\e';
        buffer += chars;
    }
    #endregion

    public static void Write(ref byte* buffer, char symbol) => Write(ref buffer, &symbol, 1);

    public static void Write(ref byte* buffer, void* array, int length) => Write(ref buffer, new Span<byte>(array, length));

    public static void Write(ref byte* buffer, ReadOnlySpan<byte> span)
    {
        var length = span.Length;
        if (length <= 8)
            *(long*)buffer = **(long**)&span;
        else span.CopyTo(new Span<byte>(buffer, int.MaxValue));
        buffer += length;
    }

    public static void Write(ref byte* buffer, Span<byte> span)
    {
        var length = span.Length;
        if (length <= 8)
            *(long*)buffer = **(long**)&span;
        span.CopyTo(new Span<byte>(buffer, int.MaxValue));
        buffer += length;
    }

    public static void WriteString(ref byte* buffer, string text)
    {
        fixed (char* chars = text)
            WriteChars(ref buffer, chars, text.Length);
    }

    public static void WriteStringLiteral(ref byte* buffer, ReadOnlySpan<char> span)
    {
        fixed (char* chars = span)
            WriteChars(ref buffer, chars, span.Length);
    }

    public static void WriteChars(ref byte* buffer, char* chars, int length)
    {
        for (var index = 0; index < length; index++)
            buffer[index] = (byte)chars[index];

        buffer += length;
    }

    public static void NewLine(ref byte* buffer) => *buffer++ = (byte)'\n';

    public static void WriteUnsignedIntegerWithLeadingZero(ref byte* buffer, ulong value, int digits)
    {
        var tempBuffer = buffer + digits - 1;
        for (var index = 0; index < digits; index++)
        {
            (value, var reminder) = Math.DivRem(value, 10);
            *tempBuffer-- = (byte)('0' + reminder);
        }

        buffer += digits;
    }

    public static void WriteUnsignedInteger(ref byte* buffer, ulong value) => WriteUnsignedIntegerWithLeadingZero(ref buffer, value, Log10(value) + 1);

    public static void WriteHexIntegerWithLeadingZero(ref byte* buffer, long value, int digits) => WriteHexIntegerWithLeadingZero(ref buffer, (ulong)value, digits);

    public static void WriteHexIntegerWithLeadingZero(ref byte* buffer, ulong value, int digits)
    {
        for (var shift = (digits - 1) << 2; shift >= 0; shift -= 4)
        {
            var digit = value >> shift & 0x0F;
            *buffer++ = (byte)(digit > 9 ? 'A' - 10 + digit : '0' + digit);
        }
    }

    public static void WriteHexInteger(ref byte* buffer, ulong value)
    {
        var shift = Math.Min(BitOperations.Log2(value), 60) & ~3;

        while (shift >= 0)
        {
            var digit = (value >> shift) & 0x0F;
            shift -= 4;

            *buffer++ = (byte)(digit > 9 ? 'A' - 10 + digit : '0' + digit);
        }
    }

    public static void WriteHexByteArray(ref byte* buffer, byte* array, int length)
    {
        for (var longIndex = 0; longIndex < length; longIndex += 8)
        {
            var byteLength = length - longIndex;
            if (byteLength > 8)
                byteLength = 8;

            var longValue = *(ulong*)&array[longIndex];
            for (var byteIndex = 0; byteIndex < byteLength; byteIndex++)
            {
                var outputValue = 0;
                var isNotEndLine = byteIndex != byteLength - 1;

                if (isNotEndLine)
                {
                    outputValue |= ' ';
                    outputValue <<= 8;
                }

                var lowDigit = (int)(longValue & 0x0F);
                longValue >>= 4;

                if (lowDigit > 9)
                    outputValue |= 'A' - 10 + lowDigit;
                else outputValue |= '0' + lowDigit;
                outputValue <<= 8;

                var highDigit = (int)(longValue & 0x0F);
                longValue >>= 4;

                if (highDigit > 9)
                    outputValue |= 'A' - 10 + highDigit;
                else outputValue |= '0' + highDigit;

                *(int*)buffer = outputValue;

                if (isNotEndLine)
                    buffer += 3;
                else buffer += 2;
            }
        }
    }

    public static void WritePointer(ref byte* buffer, nint address, char type = '0')
    {
        if (type == '0')
        {
            Write(ref buffer, "0x"u8);
            WriteHexInteger(ref buffer, (ulong)address);
        }
        else if (type == 'h')
        {
            WriteHexInteger(ref buffer, (ulong)address);
            Write(ref buffer, 'h');
        }
    }

    public static void WriteBlanks(ref byte* buffer, int count)
    {
        var blanks = "                                                                                                                                "u8;

        while (count > 0)
        {
            var bytesToWrite = Math.Min(count, blanks.Length);

            var slicedBlanks = blanks.Slice(0, bytesToWrite);
            Write(ref buffer, slicedBlanks);

            count -= bytesToWrite;
        }
    }

    static int Log10(ulong value)
    {
        var result = 0; 
        while (value > 10)
        {
            result++;
            value /= 10;
        }

        return result;
    }
}