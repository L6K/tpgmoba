using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
if (!Directory.Exists(assetsDir))
{
    Console.Error.WriteLine($"ERROR: Assets directory not found at {assetsDir}");
    return 1;
}

var csFiles = Directory.EnumerateFiles(assetsDir, "*.cs", SearchOption.AllDirectories).ToList();
var totalErrors = 0;

foreach (var file in csFiles)
{
    var source = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(source, path: file);
    var diagnostics = tree.GetDiagnostics()
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .ToList();

    foreach (var diag in diagnostics)
    {
        var span = diag.Location.GetLineSpan();
        var line = span.StartLinePosition.Line + 1;
        Console.WriteLine($"ERROR: {file}:{line}: {diag.GetMessage()}");
        totalErrors++;
    }
}

if (totalErrors > 0)
{
    Console.Error.WriteLine($"Syntax check FAILED: {totalErrors} error(s) in {csFiles.Count} file(s)");
    return 1;
}

Console.WriteLine($"OK: {csFiles.Count} files parsed");
return 0;
