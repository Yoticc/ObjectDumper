using System.Text;

/*
 * an alternative solution could be a single string and an array of formatting to it, 
 * this is faster and less memory consuming, but it seems that the algorithm for 
 * the final output to the console would be too expensive
*/
class ConsoleText
{
    public ConsoleText(IEnumerable<ConsoleTextPart> parts) => this.parts = parts;

    IEnumerable<ConsoleTextPart> parts;

    /*
     * this output method is not optimized, the optimal solution is to enable VT100 emulation support, 
     * but for this one-time output task the out-of-the-box solution will do just fine
    */
    public void DisplayOnConsole()
    {
        var oldBackground = Console.BackgroundColor;
        var oldForeground = Console.ForegroundColor;

        foreach (var part in parts)
        {
            Console.BackgroundColor = part.BackgroundColor;
            Console.ForegroundColor = part.ForegroundColor;
            Console.Write(part.Data);
        }

        Console.BackgroundColor = oldBackground;
        Console.ForegroundColor = oldForeground;
    }

    public string Serialize()
    {
        var length = 0;
        foreach (var part in parts)
            length += part.Data.Length;

        var sb = new StringBuilder(length);
        foreach (var part in parts)
            sb.Append(part);

        return sb.ToString();
    }

    public override string ToString() => Serialize();
}