using System;
using System.Text.RegularExpressions;
using Token = System.Collections.Generic.List<string?>;
using TokenList = System.Collections.Generic.List<System.Collections.Generic.List<string?>>;
using Spectre.Console;
using OneOf;

// Here is the starting point of the terminal
class Jobs
{
    static bool running = true, debug = false;
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
        Table help_table = new Table() // Table that will be shown after help command
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

        while (running)
        {
            UpdatePaths();

            Console.Write(Variables.globals["RelativePath"] + "> "); // Adding > to the end

            var input = Console.ReadLine(); // Requiring user input

            if (!string.IsNullOrWhiteSpace(input)) // Ignoring nothing or white spaces
            {
                switch (input)
                {
                    case "debug on": // Turning on debug mode //TODO: make mode a command and debug an argument
                        debug = true; // That will show Lexer's and Parser's output
                        break;
                    case "debug off": // Turning off debug mode //TODO: make mode a command and debug an argument
                        debug = false;
                        break;
                    case "quit":
                    case "exit":
                    case "close":
                    case "altf4": // Any of above can turn off Jobs //TODO: turn them to one single command
                        running = false; // Stopping loop
                        break;
                    case "help": // Writes help table //TODO: turn it to a commands
                        AnsiConsole.Write(help_table); // Writing table with AnsiConsole (Spectre.Console)
                        break;
                    default:
                        OneOf<string, Errors.BaseError> result = Compiler.JobsLang.Run(input); // Running compiler with user's input
                        if (debug) // showing logs if debug mode
                        {
                            Console.WriteLine(result.AsT0);
                        }
                        else if (result.IsT1)
                        {
                            Console.WriteLine();
                            result.AsT1.Write();
                            Console.WriteLine();
                        }
                        break;
                }
            }
        }
    }
}
class Compiler
{
    public class JobsLang
    {
        public static OneOf<string, Errors.BaseError> Run(string input)
        {
            OneOf<TokenList, Errors.BaseError> lexer_result = Lexer.Lex(input); // Recieving lexer's tokens
            
            if (lexer_result.IsT0)
            { // if there are no errors
                TokenList tokens = lexer_result.AsT0;

                string debug_token_res = "Tokens\nType: Value\n"; // Prepairing debug value

                foreach (Token token in tokens)
                {
                    debug_token_res += token[0] + ": " + token[1] + "\n"; // Filling debug
                }

                Parser parser = new(tokens); // Recieving Parser's result

                OneOf<List<ASTNode>, Errors.BaseError> ast = parser.Parse(); // Making AST

                string debug_ast = "AST:\n"; // Filling debug with AST

                if (ast.IsT1) return ast.AsT1; // if it is an error return it
                else
                {
                    foreach (ASTNode node in ast.AsT0) debug_ast += node.ToString(); // Filling debug

                    foreach (ASTNode statement in ast.AsT0) // Iterating through statements
                    {
                        Console.WriteLine();
                        OneOf<object, Errors.BaseError> result = Interpreter.Interpret(statement); // Interpreting (all results are written already in interpreter)
                        Console.Write(result.IsT1);
                        if (result.IsT1) Console.Write(result.AsT1.name, result.AsT1.message);
                        Console.WriteLine();
                    }
                }

                return debug_token_res + "\n" + debug_ast;
            }
            else return lexer_result.AsT1;
        }
    }
    public class Lexer
    {
        public static TokenList TOKENS = [ // All tokens. Commands will be here when programm will initialize
            [@"""[^""]*""", "CONTENT"],
            [@"\bwith\b", "WITH"],
            [@"\s+", null],
            [@"[^ ]*", "CONTENT"],
        ];
        public static OneOf<TokenList, Errors.BaseError> Lex(string input) // Splitting input to tokens
        {
            TokenList res_tokens = [];

            while (input != "")
            {
                bool found = false;

                foreach (Token token in TOKENS) // iterating through tokens
                {
                    string? pattern = "^" + token[0], token_type = token[1];

                    Match match = Regex.Match(input, pattern); // trying to compare pattern and input
                    if (match.Success) // if found something
                    {
                        if (token_type != null) // skip if null
                        {
                            res_tokens.Add([token_type, match.Value]); // adding token [TYPE, VALUE], e.g. ["LOG", "log"]
                        }
                        input = input[match.Length..]; // deleting matched part of the string
                        found = true;
                        break;
                    }
                }
                if (!found) // if nothing found
                {
                    return new Errors.SyntaxError($"unexpected symbol '{input[0]}'");
                }
            }
            return res_tokens;
        }
    }
    class ASTNode(string type, string? value = null)
    {
        public string? type = type, value = value;
        public List<ASTNode> children = [];

        public void Add_child(ASTNode child) {
            children.Add(child);
        }
        public string ToString(int level = 0) {
            string indent = string.Concat(Enumerable.Repeat("  ", level));
            string value_str = "";
            if (value != null) value_str = $": {value}";
            string result = $"{indent}{type}{value_str}";
            foreach (ASTNode child in children) {
                result += "\n" + child.ToString(level + 1);
            }
            return result;
        }
    }
    class Parser(TokenList tokens)
    {
        public TokenList tokens = tokens;
        public int current_token_index = 0;
        public Token? GetCurrentToken() {
            if (current_token_index < tokens.Count) return tokens[current_token_index];
            return null;
        }
        public void Consume() {
            current_token_index += 1;
        }
        public OneOf<List<ASTNode>, Errors.BaseError> Parse() {
            OneOf<List<ASTNode>, Errors.BaseError> node = ParseStatements();
            if (GetCurrentToken() != null) return new Errors.SyntaxError($"unexpected token {GetCurrentToken()[1]}");
            return node;
        }
        public OneOf<List<ASTNode>, Errors.BaseError> ParseStatements() {
            List<ASTNode> statements = [];
            Commands.BaseCommand? command = Commands.GetCommand(GetCurrentToken()[0]);
            if (command != null)
            {
                OneOf<ASTNode, Errors.BaseError> node = ParseCommand();
                if (node.IsT0)
                {
                    statements.Add(node.AsT0);
                }
                else
                {
                    return node.AsT1;
                }
            }
            else
            {
                return new Errors.SyntaxError("unknown command");
            }
            return statements;
        }
        public OneOf<ASTNode, Errors.BaseError> ParseCommand() {
            string command_name = GetCurrentToken()[0];
            Consume();
            if (GetCurrentToken() == null) return new ASTNode(command_name);

            if (GetCurrentToken()[0] == "CONTENT")
            {
                ASTNode parent_node = new(command_name);
                List<List<string>> args = []; // not token list
                
                if (GetCurrentToken()[0] != "CONTENT")
                {
                    return new Errors.SyntaxError("unexpected {GetCurrentToken()[0].ToLower()} after command. It should be some content");
                }
                args.Add(["CONTENT", GetCurrentToken()[1].Trim('"')]);
                Consume();                
                while (GetCurrentToken() != null)
                {
                    if (GetCurrentToken()[0] != "WITH")
                    {
                        return new Errors.SyntaxError($"unexpected {GetCurrentToken()[0].ToLower()} after command. It should be nothing or \"with\"");
                    }
                    Consume();
                    if (GetCurrentToken()[0] != "CONTENT")
                    {
                        return new Errors.SyntaxError($"unexpected {GetCurrentToken()[0].ToLower()} after command. It should be some content");
                    }
                    args.Add(["CONTENT", GetCurrentToken()[1].Trim('"')]);
                    Consume();
                }
                foreach (Token arg in args)
                {
                    parent_node.Add_child(new ASTNode(arg[0], arg[1]));
                }
                return parent_node;
            }
            return new Errors.SyntaxError($"unexpected {GetCurrentToken()[0].ToLower()} after command. It should be nothing or some content");
        }
    }
    class Interpreter
    {
        public static OneOf<object, Errors.BaseError> Interpret(ASTNode node)
        {
            string? command_name = node.type;
            Commands.BaseCommand? command = Commands.GetCommand(command_name);
            List<object> args = [];
            foreach (ASTNode child in node.children)
            {
                args.Add(child.value);
            }
            OneOf<object, Errors.BaseError>? result = null;
            if (command != null) result = command.Interpret(args, []);

            return result.Value != null ? result: new Errors.SyntaxError($"unknown command");
        }
    }
}

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
        public abstract OneOf<object, Errors.BaseError>? Interpret(List<object> args, Dictionary<string, object> kwargs);

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
            Description = "Writes your content into console";
            Usage = "[bold yellow]log [[[blue]any content[/]]] [white]|| [/]log [[[blue]any content[/]]] with [[[blue]any content[/]]] with [[[blue]any content[/]]]... [/]";
            Implementations = ["log", "print", "write"];
        }
        public override OneOf<object, Errors.BaseError>? Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            string content = "";

            foreach (object arg in args) content += (string)arg;
            
            Console.WriteLine(content);
            return null;
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
        public override OneOf<object, Errors.BaseError>? Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count == 1)
            {
                string new_directiory = (string)args[0];
                if (Directory.Exists(new_directiory))
                {
                    try
                    {
                        Directory.SetCurrentDirectory(new_directiory);
                        Console.WriteLine("Moved to " + new_directiory);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return new Errors.SecurityError($"Jobs don't have enough permission to get here\n");
                    }                
                }
                else
                {
                    return new Errors.SecurityError($"Jobs don't have enough permission to get here\n");    }
            }
            else
            {
                return new Errors.ArgumentsError($"commands move to needs only 1 argument as a path to your directory, not {args.Count}\n");
            }
            return null;
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
        public override OneOf<object, Errors.BaseError>? Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count == 0)
            {
                Directory.SetCurrentDirectory("..");
                string[] current_directory = Directory.GetCurrentDirectory().Split("\\");
                Console.WriteLine("Moved up to " + current_directory[current_directory.Length - 1]);
            }
            else
            {
                return new Errors.ArgumentsError($"commands move up doesn't need any arguments, got {args.Count}\n");
            }
            return null;
        }
    }
    public class Show : BaseCommand
    {
        public Show() : base()
        {
            InternalName = "SHOW";
            Description = "writes out all files or folders that are in your current folder";
            Usage = "[bold yellow]show [blue]\"files\"[/][/] or [bold yellow]show [blue]\"folders\"[/][/] or [bold yellow]show [blue]\"files\"[/] with [blue]\"nested\"[/][/]";
            Implementations = ["show", "ls", "list"];
        }
        public override OneOf<object, Errors.BaseError>? Interpret(List<object> args, Dictionary<string, object> kwargs)
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
                    AnsiConsole.Write(table);
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
                    AnsiConsole.Write(table);
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
                        }
                    });
                    AnsiConsole.Write(table);
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
            return null;
        }
    }
}
class Errors
{
    abstract public class BaseError(string error_name, string msg)
    {
        public string name = error_name, message = msg;

        public void Write()
        {
            AnsiConsole.Markup($"[red]{name}[/]: {message}\n");
        }
    }
    public class SyntaxError(string msg) : BaseError("Incorrect syntax", msg) {}
    public class SecurityError(string msg) : BaseError("Security error", msg) {}
    public class ArgumentsError(string msg) : BaseError("Arguments' error", msg) { }
}
class Variables
{
    public static Dictionary<string, object?> globals = new()
    {
        {"Running", true },
        {"DebugMode", false},

        // Paths
        {"AbsolutePath", null},
        {"RelativePath", null},
    };
}