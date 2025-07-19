struct ConsoleTextPart
{
    public ConsoleTextPart(string data, ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black) 
        => (Data, ForegroundColor, BackgroundColor) = (data, foreground, background);

    public string Data;
    public ConsoleColor ForegroundColor, BackgroundColor;
}