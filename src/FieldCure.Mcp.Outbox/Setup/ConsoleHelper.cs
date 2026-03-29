namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Console I/O helpers for the interactive setup flow.
/// </summary>
public static class ConsoleHelper
{
    /// <summary>
    /// Prints a formatted section header to the console.
    /// </summary>
    /// <param name="title">The header title text.</param>
    public static void PrintHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== FieldCure Outbox - {title} ===");
        Console.WriteLine();
    }

    /// <summary>
    /// Prompts the user for input and returns the trimmed response.
    /// </summary>
    /// <param name="prompt">The prompt label to display.</param>
    public static string ReadLine(string prompt)
    {
        Console.Write($"{prompt}: ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Prompts the user for input, returning a default value if input is empty.
    /// </summary>
    /// <param name="prompt">The prompt label to display.</param>
    /// <param name="defaultValue">The value to return when the user presses Enter without typing.</param>
    public static string ReadLineWithDefault(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    /// <summary>
    /// Prompts the user for sensitive input, masking characters with asterisks.
    /// </summary>
    /// <param name="prompt">The prompt label to display.</param>
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

        return new string([.. input]).Trim();
    }

    /// <summary>
    /// Prints a success message prefixed with [OK].
    /// </summary>
    /// <param name="message">The success message text.</param>
    public static void PrintSuccess(string message)
    {
        Console.WriteLine($"[OK] {message}");
    }

    /// <summary>
    /// Prints an error message to the console.
    /// </summary>
    /// <param name="message">The error message text.</param>
    public static void PrintError(string message)
    {
        Console.WriteLine($"Error: {message}");
    }

    /// <summary>
    /// Waits for the user to press any key before continuing.
    /// </summary>
    public static void WaitForKey()
    {
        Console.WriteLine();
        Console.Write("Press any key to close...");
        Console.ReadKey(intercept: true);
    }
}
