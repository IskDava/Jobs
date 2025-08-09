using OneOf;
using System.Text.RegularExpressions;
using Token = System.Collections.Generic.List<string?>;
using TokenList = System.Collections.Generic.List<System.Collections.Generic.List<string?>>;

class Compiler
{
    public class JobsLang
    {
        public static OneOf<List<Result>, Errors.BaseError> Run(string input)
        {
            OneOf<TokenList, Errors.BaseError> lexer_result = Lexer.Lex(input); // Recieving lexer's tokens

            if (lexer_result.IsT0)
            { // if there are no errors
                TokenList tokens = lexer_result.AsT0;

                Parser parser = new(tokens); // Recieving Parser's result

                OneOf<List<ASTNode>, Errors.BaseError> ast = parser.Parse(); // Making AST

                List<Result> output = [];

                if (ast.IsT1) return ast.AsT1; // if it is an error return it
                else
                {
                    foreach (ASTNode statement in ast.AsT0) // Iterating through statements
                    {
                        OneOf<Result, Errors.BaseError> result = Interpreter.Interpret(statement); // Interpreting (all results are written already in interpreter)

                        if (result.IsT1) return result.AsT1;
                        else output.Add(result.AsT0);
                    }
                }

                return output;
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

        public void Add_child(ASTNode child)
        {
            children.Add(child);
        }
        public string ToString(int level = 0)
        {
            string indent = string.Concat(Enumerable.Repeat("  ", level));
            string value_str = "";
            if (value != null) value_str = $": {value}";
            string result = $"{indent}{type}{value_str}";
            foreach (ASTNode child in children)
            {
                result += "\n" + child.ToString(level + 1);
            }
            return result;
        }
    }
    class Parser(TokenList tokens)
    {
        public TokenList tokens = tokens;
        public int current_token_index = 0;
        public Token? GetCurrentToken()
        {
            if (current_token_index < tokens.Count) return tokens[current_token_index];
            return null;
        }
        public void Consume()
        {
            current_token_index += 1;
        }
        public OneOf<List<ASTNode>, Errors.BaseError> Parse()
        {
            OneOf<List<ASTNode>, Errors.BaseError> node = ParseStatements();
            if (GetCurrentToken() != null) return new Errors.SyntaxError($"unexpected token {GetCurrentToken()[1]}");
            return node;
        }
        public OneOf<List<ASTNode>, Errors.BaseError> ParseStatements()
        {
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
        public OneOf<ASTNode, Errors.BaseError> ParseCommand()
        {
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
        public static OneOf<Result, Errors.BaseError> Interpret(ASTNode node)
        {
            string? command_name = node.type;
            Commands.BaseCommand? command = Commands.GetCommand(command_name);
            List<object> args = [];
            foreach (ASTNode child in node.children) args.Add(child.value);

            OneOf<Result, Errors.BaseError> result = new Errors.SyntaxError($"command {command_name} not found");
            if (command != null) result = command.Interpret(args, []);

            return result;
        }
    }
}
