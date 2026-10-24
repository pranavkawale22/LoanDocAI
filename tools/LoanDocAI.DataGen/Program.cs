using System.Text;
using LoanDocAI.DataGen;

// Writes data/synthetic/pdfs/<id>.pdf and data/synthetic/truth/<id>.json under the repository root.
var root = FindRepositoryRoot(Directory.GetCurrentDirectory());
if (root is null)
{
    Console.Error.WriteLine(
        "Could not find LoanDocAI.sln by walking up from " + Directory.GetCurrentDirectory() +
        ". Run this tool from inside the LoanDocAI repository.");
    return 1;
}

var pdfDir = Path.Combine(root, "data", "synthetic", "pdfs");
var truthDir = Path.Combine(root, "data", "synthetic", "truth");
Directory.CreateDirectory(pdfDir);
Directory.CreateDirectory(truthDir);

var docs = SyntheticDataGenerator.Generate();
foreach (var doc in docs)
{
    File.WriteAllBytes(Path.Combine(pdfDir, doc.Id + ".pdf"), PdfRenderer.Render(doc));
    File.WriteAllText(Path.Combine(truthDir, doc.Id + ".json"), SyntheticDataGenerator.ToTruthJson(doc), new UTF8Encoding(false));
}

Console.WriteLine($"Wrote {docs.Count} PDFs and {docs.Count} truth files under {Path.Combine(root, "data", "synthetic")}");
return 0;

static string? FindRepositoryRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "LoanDocAI.sln")))
            return dir.FullName;
    return null;
}
