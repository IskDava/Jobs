using System;
using System.Linq;
using System.Text.RegularExpressions;
using TokenList = System.Collections.Generic.List<System.Collections.Generic.List<string>>;
using Token = System.Collections.Generic.List<string>;
using Spectre.Console;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604

class Jobs
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Jobs terminal");
        Console.WriteLine("If you are new to Jobs type \"help\" for mini-guide");
        Console.WriteLine("By David Iskiev (Dava), 2025\n\n");
        bool running = true, debug = false;
        while (running)
        {
            string absolute_path = Directory.GetCurrentDirectory();
            string[] path_parts = absolute_path.Split(Path.DirectorySeparatorChar);
            string relative_path = "";
            if (path_parts.Length == 1)
            {
                relative_path = path_parts[0];
            }
            else if (path_parts.Length == 2)
            {
                relative_path = Path.Combine(path_parts[0], path_parts[1]);
            }
            else
            {
                relative_path = "..." + Path.DirectorySeparatorChar + Path.Combine(path_parts[path_parts.Length - 2], path_parts[path_parts.Length - 1]);
            }
            Console.Write(relative_path + "> ");
            var input = Console.ReadLine();
            if (input != null)
            {
                switch (input)
                {
                    case "debug mode":
                        debug = true;
                        break;
                    case "user mode":
                        debug = false;
                        break;
                    case "quit":
                        running = false;
                        break;
                    case "help":
                        Console.WriteLine("Here are some commands:");
                        AnsiConsole.Write(
                            new Table()
                                .Border(TableBorder.Rounded)
                                .AddColumn("[green]Command[/]")
                                .AddColumn("[red]Action[/]")
                                .AddRow("help", "mini-guide")
                                .AddRow("quit", "exits the terminal")
                                .AddRow("debug mode", "shows the Parser's and Lexer's result (for developers)")
                                .AddRow("user mode", "turns off debug mode")
                                .AddRow("log", "writes some content in the console. log \"smth\" => smth")
                                .AddRow("move to (or cd)", "changes current directory. move to \"path\\to\\directory\"")
                                .AddRow("move up", "changes current directory to the upper one. move up = move to \"..\"")
                                );
                        break;
                    case "":
                        break;
                    default:
                        (string, string) result = Compiler.JobsLang.Run(input);
                        string status = result.Item1;
                        if (debug)
                        {
                            Console.WriteLine(result.Item2);
                        }
                        else if (status != "success")
                        {
                            string[] err = result.Item2.Split("|");
                            AnsiConsole.Markup($"\n[red bold]{err[1]}:[/] {err[2]}\n\n");
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
        public static (string, string) Run(string input)
        {
            (string, TokenList?) lexer_result = Lexer.Lex(input);
            string res_status = lexer_result.Item1;
            if (res_status == "success") {
                TokenList? tokens = lexer_result.Item2;
                string debug_token_res = "Tokens\nType: Value\n";
                foreach (Token token in tokens)
                {
                    debug_token_res += token[0] + ": " + token[1] + "\n";
                }
                Parser parser = new(tokens);
                (string, List<ASTNode>?) ast = parser.parse();
                string debug_ast = "AST:\n";
                if (ast.Item1 != "success") return ("error", ast.Item1);
                else
                {
                    foreach (ASTNode node in ast.Item2)
                    {
                        debug_ast += node.ToString();
                    }
                    Interpreter interpreter = new();
                    foreach (ASTNode statement in ast.Item2)
                    {
                        Console.WriteLine();
                        interpreter.Interpret(statement);
                        Console.WriteLine();
                    }
                }
                
                return ("success", debug_token_res + "\n" + debug_ast);
            }
            else
            {
                return ("error", res_status);
            }
        }
    }
    class Lexer
    {
        private readonly static string?[][] TOKENS = [
            [@"\blog\b", "LOG"],
            [@"\bmove to\b", "MOVETO"],
            [@"\bcd\b", "MOVETO"],
            [@"\bmove up\b", "MOVEUP"],
            [@"\bshow files\b", "SHOWFILES"],
            [@"\bshow folders\b", "SHOWFOLDERS"],
            [@"\bshow all files\b", "SHOWALLFILES"],
            [@"""[^""]+""", "CONTENT"],
            [@"\bwith\b", "WITH"],
            [@"\s+", null],
        ];
        public static (string, TokenList?) Lex(string input)
        {
            TokenList res_tokens = [];
            while (input != "") {
                bool found = false;
                foreach (string?[] token in TOKENS) {
                    string? pattern = "^" + token[0], token_type = token[1];
                    Match match = Regex.Match(input, pattern);
                    if (match.Success) {
                        if (token_type != null) {
                            res_tokens.Add([token_type, match.Value]);
                        }
                        input = input[match.Length..];
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    (string, TokenList?) value = ($"error|SyntaxTrouble|unexpected symbol '{input[0]}'", null);
                    return value;
                }
            }
            return ("success", res_tokens);
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
        public Token? get_current_token() {
            if (current_token_index < tokens.Count) return tokens[current_token_index];
            return null;
        }
        public void consume() {
            current_token_index += 1;
        }

        public (string, List<ASTNode>?) parse() {
            (string, List<ASTNode>?) node = parse_statements();
            if (get_current_token() != null) return ($"error|unexpected token| {get_current_token()[1]}", null);
            return node;
        }
        public (string, List<ASTNode>?) parse_statements() {
            List<ASTNode> statements = [];
            Commands.BaseCommand? command = Commands.GetCommand(get_current_token()[0]);
            if (command != null)
            {
                var node = parse_command();
                if (node.Item1 == "success")
                {
                    statements.Add(node.Item2);
                }
                else
                {
                    return (node.Item1, null);
                }
            }
            else
            {
                return ("error|unknown command", null);
            }
            return ("success", statements);
        }
        public (string, ASTNode?) parse_command() {
            string command_name = get_current_token()[0];
            consume();
            if (get_current_token() == null)
            {
                return ("success", new ASTNode(command_name));
            }
            if (get_current_token()[0] == "CONTENT")
            {
                ASTNode parent_node = new(command_name);
                TokenList args = []; // not token list
                
                if (get_current_token()[0] != "CONTENT")
                {
                    return ($"error|unexpected {get_current_token()[0].ToLower()} after command. It should be some content", null);
                }
                args.Add(["CONTENT", get_current_token()[1].Trim('"')]);
                consume();                
                while (get_current_token() != null)
                {
                    if (get_current_token()[0] != "WITH")
                    {
                        return ($"error|unexpected {get_current_token()[0].ToLower()} after command. It should be nothing or 'with'",
                null);
                    }
                    consume();
                    if (get_current_token()[0] != "CONTENT")
                    {
                        return ($"error|unexpected {get_current_token()[0].ToLower()} after command. It should be some content",
                null);
                    }
                    args.Add(["CONTENT", get_current_token()[1].Trim('"')]);
                    consume();
                }
                List<ASTNode> ast_args = [];
                foreach (Token arg in args)
                {
                    parent_node.Add_child(new ASTNode(arg[0], arg[1]));
                }
                return ("success", parent_node);
            }
            return ($"error|unexpected {get_current_token()[0].ToLower()} after command. It should be nothing or some content",
                null);
        }
    }
    class Interpreter
    {
        public object? Interpret(ASTNode node)
        {
            string? command_name = node.type;
            Commands.BaseCommand? command = Commands.GetCommand(command_name);
            List<object> args = [];
            foreach (ASTNode child in node.children)
            {
                args.Add(child.value);
            }
            object? result = null;
            if (command != null)
            {
                result = command.Interpret(args, []);
            }
            return result;
        }
    }
}

class Commands
{
    private static readonly List<BaseCommand> CommandsList = [new Log(), new MoveTo(), new MoveUp(), new ShowFiles(), new ShowFolders(), new ShowAllFiles()]; 
    public static List<BaseCommand> Dir()
    {
        return CommandsList;
    } 
    public static BaseCommand? GetCommand(string? name)
    {
        if (name == null) return null;
        foreach (BaseCommand command in Dir())
        {
            if (name == command.name) return command;
        }
        return null;
    }
    abstract public class BaseCommand()
    {
        public string? name;
        public int required_arguments = 0, unrequired_arguments = 0;

        public abstract object? Interpret(List<object> args, Dictionary<string, object> kwargs);
    }
    public class Log : BaseCommand
    {
        public Log() : base()
        {
            name = "LOG";
        }
        public override object? Interpret(List<object> args, Dictionary<string, object> kwargs) 
        {
            string content = "";
            foreach (object arg in args)
            {
                content += (string)arg;
            }
            Console.WriteLine(content);
            return null;
        }
    }
    public class MoveTo : BaseCommand
    {
        public MoveTo() : base()
        {
            name = "MOVETO";
        }
        public override object? Interpret(List<object> args, Dictionary<string, object> kwargs)
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
                        AnsiConsole.Markup($"[red bold]SecurityFailure:[/] Jobs don't have enough permission to get here ({Directory.GetCurrentDirectory() + "\\" + new_directiory})\n");
                    }
                }
                else
                {
                    AnsiConsole.Markup($"[red bold]DirectoryNotFound:[/] that directory ({new_directiory}) does not exist here\n");
                }
            }
            else
            {
                AnsiConsole.Markup($"[red bold]ArgumentOverflow:[/] commands move to needs only 1 argument -- path to your directory, not {args.Count}\n");
            }
            return null;
        }
    }
    public class MoveUp : BaseCommand
    {
        public MoveUp() : base()
        {
            name = "MOVEUP";
        }
        public override object? Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count == 0)
            {
                Directory.SetCurrentDirectory("..");
                string[] current_directory = Directory.GetCurrentDirectory().Split("\\");
                Console.WriteLine("Moved up to " + current_directory[current_directory.Length - 1]);
            }
            else
            {
                AnsiConsole.Markup($"[red bold]ArgumentOverflow:[/] commands move up doesn't need any arguments, got {args.Count}\n");
            }
            return null;
        }
    }

    public class ShowFiles : BaseCommand
    {
        public ShowFiles() : base()
        {
            name = "SHOWFILES";
        }
        public override object? Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count() == 0)
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
            else
            {
                AnsiConsole.Markup($"[red bold]ArgumentOverflow:[/] commands show files doesn't need any arguments, got {args.Count}\n");
            }
            return null;
        }
    }
    public class ShowFolders : BaseCommand
    {
        public ShowFolders() : base()
        {
            name = "SHOWFOLDERS";
        }
        public override object? Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count() == 0)
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
                AnsiConsole.Markup($"[red bold]ArgumentOverflow:[/] commands show folders doesn't need any arguments, got {args.Count}\n");
            }
            return null;
        }
    }
    public class ShowAllFiles : BaseCommand
    {
        public ShowAllFiles() : base()
        {
            name = "SHOWALLFILES";
        }
        public override object? Interpret(List<object> args, Dictionary<string, object> kwargs)
        {
            if (args.Count() == 0)
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
                AnsiConsole.Markup($"[red bold]ArgumentOverflow:[/] commands show all files doesn't need any arguments, got {args.Count}\n");
            }
            return null;
        }
    }
}