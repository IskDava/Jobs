using OneOf;
using Spectre.Console;

class Commands
{
    public static List<BaseCommand> CommandsList = [];
    public static BaseCommand? GetCommand(string? key)
    {
        if (key == null) return null;
        foreach (BaseCommand command in CommandsList)
        {
            if (key == command.InternalName) return command;
        }
        return null;
    }
    abstract public class BaseCommand
    {
        public required string InternalName, Description, Usage;
        // Internal names are used by programm in regex or searching. Should be written in caps
        // Friendly names are made automaticly (based on Iplementations) and written in documentaitions and represent common way to write the command and its variant (common_name (or another_name, another_name ...))
        // Description is used in documentation to describe the action that command does. It shouldn't have any colors
        // Usage is used in documentation to provide examples of command. I don't recommend to put more than 5 examples, but at least one is essential. Should be collorized in accordance with JHR 
        // RegexContent is created by programm automaticly to work with Lexer. You shouldn't change it
        public required string[] Implementations;
        // Implementations should contain ALL possible names for your command in a string array, where first one is common one
        public int required_arguments = 0, unrequired_arguments = 0;
        public abstract OneOf<Result, Errors.BaseError> Interpret(List<object> args, Dictionary<string, object> kwargs);

        public string GetRegex()
        {
            string RegexContent = @"\b(";

            foreach (string name in Implementations) RegexContent += name + "|";

            RegexContent = RegexContent[..^1];
            RegexContent += @")\b";
            return RegexContent;
        }
        public string GetFriendlyName()
        {
            string FriendlyName = Implementations[0];
            if (Implementations.Length > 1)
            {
                FriendlyName += " (or ";
                foreach (string name in Implementations[^(Implementations.Length - 1)..])
                {
                    FriendlyName += name + ", ";
                }
                FriendlyName = FriendlyName[..^2];
                FriendlyName += ")";
            }
            return FriendlyName;
        }
    }
    public class Log : BaseCommand
    {
        public Log() : base()
        {
            InternalName = "LOG";
            Description = "writes your content into console";
            Usage = "[bold yellow]log [[[blue]any content[/]]] [white]|| [/]log [[[blue]any content[/]]] with [[[blue]any content[/]]] with [[[blue]any content[/]]]... [/]";
            Implementations = ["log", "print", "write"];
        }
        public override OneOf<Result, Errors.BaseError> Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            Console.WriteLine();
            string content = "";

            foreach (object arg in args) content += (string)arg;

            Console.WriteLine(content);
            Console.WriteLine();

            return new Result(content);
        }
    }
    public class MoveTo : BaseCommand
    {
        public MoveTo() : base()
        {
            InternalName = "MOVETO";
            Description = "changes current folder you are in. Affects on relative paths";
            Usage = "[bold yellow]move to [[[blue]next folder[/]]] [white]|| [/]move to [blue]\"..\"[/][white] (moves up one directory) ||[/] move to [[[blue]path/some/where[/]]][/]";
            Implementations = ["move to", "cd"];
        }
        public override OneOf<Result, Errors.BaseError> Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count == 1)
            {
                string new_directiory = (string)args[0];
                if (Directory.Exists(new_directiory))
                {
                    try
                    {
                        Directory.SetCurrentDirectory(new_directiory);

                        Console.WriteLine();
                        Console.WriteLine("Moved to " + new_directiory);
                        Console.WriteLine();

                        return new Result(new_directiory);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return new Errors.SecurityError($"Jobs don't have enough permission to get in {new_directiory} folder");
                    }
                }
                else
                {
                    return new Errors.SecurityError($"This folder ({new_directiory}) does not exist");
                }
            }
            else
            {
                return new Errors.ArgumentsError($"commands move to needs only 1 argument as a path to your directory, not {args.Count}");
            }
        }
    }
    public class MoveUp : BaseCommand
    {
        public MoveUp() : base()
        {
            InternalName = "MOVEUP";
            Description = "change folder to upper one. Works like (move to \"..\")";
            Usage = "[bold yellow]move up[/]";
            Implementations = ["move up", "move back"];
        }
        public override OneOf<Result, Errors.BaseError> Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count == 0)
            {
                Directory.SetCurrentDirectory("..");
                string[] current_directory = Directory.GetCurrentDirectory().Split("\\");
                string new_directory = current_directory[current_directory.Length - 1];

                Console.WriteLine();
                Console.WriteLine("Moved up to " + new_directory);
                Console.WriteLine();

                return new Result(new_directory);
            }
            else
            {
                return new Errors.ArgumentsError($"commands move up doesn't need any arguments, got {args.Count}\n");
            }
        }
    }
    public class Show : BaseCommand
    {
        public Show() : base()
        {
            InternalName = "SHOW";
            Description = "writes out all files or folders that are in your current folder";
            Usage = "[bold yellow]show [blue]\"files\"[/][/] || [bold yellow]show [blue]\"folders\"[/][/] || [bold yellow]show [blue]\"files\"[/] with [blue]\"nested\"[/][/]";
            Implementations = ["show", "ls", "list"];
        }
        public override OneOf<Result, Errors.BaseError> Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count == 1)
            {
                if ((string)args[0] == "files")
                {
                    var opts = new EnumerationOptions
                    {
                        IgnoreInaccessible = true
                    };
                    List<string> files = [.. Directory.GetFiles(Directory.GetCurrentDirectory(), "*", opts)];
                    Table table = new Table().Border(TableBorder.Rounded).AddColumn("[green bold]File[/]");
                    foreach (string file in files)
                    {
                        string[] path_parts = file.Split(Path.DirectorySeparatorChar);
                        table.AddRow(path_parts[path_parts.Length - 1]);
                    }

                    Console.WriteLine();
                    AnsiConsole.Write(table);
                    Console.WriteLine();

                    return new Result(files);
                }
                else if ((string)args[0] == "folders")
                {
                    var opts = new EnumerationOptions
                    {
                        IgnoreInaccessible = true
                    };
                    List<string> files = [.. Directory.GetDirectories(Directory.GetCurrentDirectory(), "*", opts)];
                    Table table = new Table().Border(TableBorder.Rounded).AddColumn("[green bold]Folder[/]");
                    foreach (string file in files)
                    {
                        string[] path_parts = file.Split(Path.DirectorySeparatorChar);
                        table.AddRow(path_parts[path_parts.Length - 1]);
                    }

                    Console.WriteLine();
                    AnsiConsole.Write(table);
                    Console.WriteLine();

                    return new Result(files);
                }
                else
                {
                    return new Errors.ArgumentsError($"first argument ({args[0]}) should be either \"files\" or \"folders\"");
                }
            }
            else if (args.Count == 2)
            {
                if ((string)args[0] == "files" && (string)args[1] == "nested")
                {
                    List<string> files = [];
                    var opts = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true
                    };
                    Table table = new Table().Border(TableBorder.Rounded).AddColumn("[green bold]File[/]");
                    AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start("Collecting files...", ctx =>
                    {
                        foreach (string file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", opts))
                        {
                            string[] path_parts = file.Split(Path.DirectorySeparatorChar);
                            table.AddRow(Markup.Escape(path_parts[path_parts.Length - 1]));
                            files.Add(file);
                        }
                    });

                    Console.WriteLine();
                    AnsiConsole.Write(table);
                    Console.WriteLine();

                    return new Result(files);
                }
                else
                {
                    return new Errors.ArgumentsError($"in that case first and second arguments can't be {args[0]} and {args[1]}");
                }
            }
            else
            {
                return new Errors.ArgumentsError($"commands need 0 arguments, got {args.Count}\n");
            }
        }
    }
    public class Help : BaseCommand
    {
        public Help() : base()
        {
            InternalName = "HELP";
            Description = "shows a table with all commands and their documetnation";
            Usage = "[bold yellow]help[/]";
            Implementations = ["help"];
        }
        public override OneOf<Result, Errors.BaseError> Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            Console.WriteLine();
            AnsiConsole.Write(Jobs.help_table);
            Console.WriteLine();

            return new Result(Jobs.help_table);
        }
    }
    public class Exit : BaseCommand
    {
        public Exit() : base()
        {
            InternalName = "EXIT";
            Description = "closes the terminal";
            Usage = "[bold yellow]exit[/]";
            Implementations = ["exit", "quit", "logout", "close", "altf4"];
        }
        public override OneOf<Result, Errors.BaseError> Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            Variables.globals["Running"] = false;
            return new Result(true);
        }
    }
}
