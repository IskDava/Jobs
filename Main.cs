using System;
using System.Text.RegularExpressions;
using Spectre.Console;
using OneOf;

// Here is the starting point of the terminal
class Jobs
{
    public static Table help_table;
    // Updating abslute and relative path's global variables
    static void UpdatePaths()
    {
        string absolute_path = Directory.GetCurrentDirectory(); 
        string[] path_parts = absolute_path.Split(Path.DirectorySeparatorChar);
        string relative_path;

        if (path_parts.Length == 1) relative_path = path_parts[0];

        else if (path_parts.Length == 2) relative_path = Path.Combine(path_parts[0], path_parts[1]);

        else relative_path = "..." + Path.DirectorySeparatorChar + Path.Combine(path_parts[^2], path_parts[^1]);

        Variables.globals["AbsolutePath"] = absolute_path;
        Variables.globals["RelativePath"] = relative_path;
    }
    static void Main() // Starting point
    {
        help_table = new Table() // Table that will be shown after help command
            .Border(TableBorder.Rounded)
            .AddColumn("[green]Command[/]")
            .AddColumn("[red]Action[/]")
            .AddColumn("[yellow]Usage[/]");

        Type baseType = typeof(Commands.BaseCommand);

        Commands.CommandsList = [.. AppDomain.CurrentDomain // Filling command's list with object that are instances of BaseCommand
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass
                    && !t.IsAbstract
                    && baseType.IsAssignableFrom(t)
                    && t.GetConstructor(Type.EmptyTypes) != null
                )
            .Select(t => (Commands.BaseCommand)Activator.CreateInstance(t)!)];

        foreach (Commands.BaseCommand command in Commands.CommandsList)
        {
            Compiler.Lexer.TOKENS.Insert(0, [command.GetRegex(), command.InternalName]); // Filling token's list with commands
            help_table // Filliing table with commands, their description and usage
                .AddRow("[yellow]" + command.GetFriendlyName() + "[/]", command.Description, command.Usage)
                .AddRow("");
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8; // Changing encoding for UTF8

        Console.WriteLine("Jobs terminal");
        Console.WriteLine("If you are new to Jobs type \"help\" for mini-guide");
        Console.WriteLine("By David Iskiev (Dava), 2025\n\n"); // Welcome message

        while ((bool)Variables.globals["Running"])
        {
            UpdatePaths();

            Console.Write(Variables.globals["RelativePath"] + "> "); // Adding > to the end

            var input = Console.ReadLine(); // Requiring user input

            if (!string.IsNullOrWhiteSpace(input)) // Ignoring nothing or white spaces
            {
                OneOf<List<Result>, Errors.BaseError> result = Compiler.JobsLang.Run(input); // Running compiler with user's input
                if (result.IsT1)
                {
                    result.AsT1.Write();
                }
            }
        }
    }
}
//TODO: Status codes