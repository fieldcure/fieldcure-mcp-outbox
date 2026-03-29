namespace FieldCure.Mcp.Outbox.Setup;

public static class ConsoleHelper
{
    public static void PrintHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== FieldCure Outbox - {title} ===");
        Console.WriteLine();
    }

    public static string ReadLine(string prompt)
    {
        Console.Write($"{prompt}: ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    public static string ReadLineWithDefault(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    public static string ReadMasked(string prompt)
    {
        Console.Write($"{prompt}: ");

        var input = new List<char>();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (input.Count > 0)
                {
                    input.RemoveAt(input.Count - 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input.Add(key.KeyChar);
                Console.Write('*');
            }
        }

        return new string(input.ToArray()).Trim();
    }

    public static void PrintSuccess(string message)
    {
        Console.WriteLine($"[OK] {message}");
    }

    public static void PrintError(string message)
    {
        Console.WriteLine($"Error: {message}");
    }

    public static void WaitForKey()
    {
        Console.WriteLine();
        Console.Write("Press any key to close...");
        Console.ReadKey(intercept: true);
    }
}
