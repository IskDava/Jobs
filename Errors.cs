using Spectre.Console;

class Errors
{
    abstract public class BaseError(string error_name, string msg)
    {
        public string name = error_name, message = msg;

        public void Write()
        {
            AnsiConsole.Markup($"\n[red]{name}[/]: {message}\n\n");
        }
    }
    public class SyntaxError(string msg) : BaseError("Incorrect syntax", msg) { }
    public class SecurityError(string msg) : BaseError("Security error", msg) { }
    public class ArgumentsError(string msg) : BaseError("Arguments' error", msg) { }
}
