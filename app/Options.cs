using CommandLine;

namespace VarjoDataLogger;

public class Options
{
    [Option('a', "ip", Required = false, HelpText = "IP address of the PC running N-Back task application.")]
    public string IP { get; set; } = "127.0.0.1";

    public static Options Parse(string[] args)
    {
        var options = Parser.Default.ParseArguments<Options>(args);
        options.WithNotParsed(errors =>
            {
                Console.WriteLine("Failed to parse argument(s):");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  {error}");
                }
            });

        return options.Value;
    }
}