using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SbFormat;

// Base: Streamer.bot Format Document (Roslyn NormalizeWhitespace) — compact, CRLF, 4 spaces.
// Extra (local only): re-expand FluentConfig fluent chains into Laravel-style line breaks.

const string Indentation = "    ";
const string Eol = "\r\n";

static string Format(string code)
{
    var root = CSharpSyntaxTree
        .ParseText(code)
        .GetRoot()
        .NormalizeWhitespace(indentation: Indentation, eol: Eol, elasticTrivia: false);

    root = FluentConfigLaravelFormatter.Apply(root);
    return root.ToFullString();
}

static void PrintHelp()
{
    Console.Error.WriteLine(
        """
        SbFormat — Streamer.bot base format + FluentConfig Laravel-style chains

        Usage:
          SbFormat --stdin              Read source from stdin, write formatted source to stdout
          SbFormat <file> [<file>...]   Format files in place
          SbFormat --check <file>...    Exit 1 if any file would change (no writes)
          SbFormat --help               Show this help
        """
    );
}

var argsList = args.ToList();
if (argsList.Count == 0 || argsList.Contains("-h") || argsList.Contains("--help"))
{
    PrintHelp();
    return argsList.Count == 0 ? 1 : 0;
}

if (argsList.Contains("--stdin"))
{
    using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
    var input = reader.ReadToEnd();
    Console.Write(Format(input));
    return 0;
}

var checkOnly = argsList.RemoveAll(a => a == "--check") > 0;
var files = argsList.Where(a => !a.StartsWith('-')).ToList();
if (files.Count == 0)
{
    PrintHelp();
    return 1;
}

var changed = 0;
foreach (var file in files)
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"File not found: {file}");
        return 2;
    }

    var original = File.ReadAllText(file);
    var formatted = Format(original);
    if (string.Equals(original, formatted, StringComparison.Ordinal))
        continue;

    changed++;
    if (checkOnly)
    {
        Console.Error.WriteLine($"Would format: {file}");
        continue;
    }

    var hasBom = original.Length > 0 && original[0] == '\uFEFF';
    var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom);
    File.WriteAllText(file, formatted, encoding);
    Console.Error.WriteLine($"Formatted: {file}");
}

return checkOnly && changed > 0 ? 1 : 0;
