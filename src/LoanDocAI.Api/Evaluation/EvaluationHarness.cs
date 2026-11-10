using System.Text.Json;
using System.Text.RegularExpressions;
using LoanDocAI.Api.Models;

namespace LoanDocAI.Api.Evaluation;

public record FieldScore(string FieldName, bool IsMatch, object? Expected, object? Actual);

public record DocumentEvaluationResult(
    string DocumentId,
    string DocumentType,
    IReadOnlyList<FieldScore> FieldScores
)
{
    public int TotalFields => FieldScores.Count;
    public int CorrectFields => FieldScores.Count(f => f.IsMatch);
    public double Accuracy => TotalFields == 0 ? 0.0 : (double)CorrectFields / TotalFields;
}

public record EvaluationReport(
    IReadOnlyList<DocumentEvaluationResult> DocumentResults,
    Dictionary<string, double> FieldAccuracies,
    Dictionary<string, double> DocumentTypeAccuracies,
    double OverallAccuracy
)
{
    public void PrintSummary(TextWriter writer)
    {
        writer.WriteLine("=================================================================");
        writer.WriteLine("                   EVALUATION HARNESS REPORT                     ");
        writer.WriteLine("=================================================================");
        writer.WriteLine($"Total Documents Evaluated: {DocumentResults.Count}");
        writer.WriteLine($"Overall Accuracy:          {OverallAccuracy * 100:F2}%");
        writer.WriteLine("-----------------------------------------------------------------");
        writer.WriteLine("Accuracy by Document Type:");
        foreach (var (docType, acc) in DocumentTypeAccuracies)
        {
            writer.WriteLine($"  - {docType,-18}: {acc * 100:F2}%");
        }
        writer.WriteLine("-----------------------------------------------------------------");
        writer.WriteLine("Accuracy by Field:");
        foreach (var (field, acc) in FieldAccuracies.OrderBy(kv => kv.Key))
        {
            writer.WriteLine($"  - {field,-18}: {acc * 100:F2}%");
        }
        writer.WriteLine("=================================================================");

        var failures = DocumentResults
            .SelectMany(d => d.FieldScores.Where(f => !f.IsMatch).Select(f => (d.DocumentId, f)))
            .ToList();

        if (failures.Count > 0)
        {
            writer.WriteLine($"\nField Discrepancies ({failures.Count} total):");
            foreach (var (docId, f) in failures)
            {
                writer.WriteLine($"  [{docId}] {f.FieldName}: Expected '{f.Expected}', Got '{f.Actual}'");
            }
        }
        else
        {
            writer.WriteLine("\n[PERFECT MATCH] All fields matched ground-truth with 100% accuracy!");
        }
    }
}

public static class EvaluationHarness
{
    public static DocumentEvaluationResult Evaluate(string documentId, JsonElement truth, DocumentExtractionResult? actual)
    {
        var scores = new List<FieldScore>();

        string truthDocType = truth.GetProperty("document_type").GetString()!;

        // 1. Common fields
        scores.Add(CompareStrings("document_type", truthDocType, actual?.DocumentType));
        scores.Add(CompareStrings("applicant_name", truth.GetProperty("applicant_name").GetString(), actual?.ApplicantName));

        if (truthDocType == DocumentTypes.SalarySlip)
        {
            var slip = actual as SalarySlipExtraction;
            scores.Add(CompareStrings("employer_name", truth.GetProperty("employer_name").GetString(), slip?.EmployerName));
            scores.Add(CompareExact("pay_month", truth.GetProperty("pay_month").GetString(), slip?.PayMonth));
            scores.Add(CompareMoney("gross_salary", truth.GetProperty("gross_salary").GetDecimal(), slip?.GrossSalary));
            scores.Add(CompareMoney("net_salary", truth.GetProperty("net_salary").GetDecimal(), slip?.NetSalary));
        }
        else if (truthDocType == DocumentTypes.BankStatement)
        {
            var stmt = actual as BankStatementExtraction;
            scores.Add(CompareStrings("bank_name", truth.GetProperty("bank_name").GetString(), stmt?.BankName));
            scores.Add(CompareExact("statement_from", truth.GetProperty("statement_from").GetString(), stmt?.StatementFrom));
            scores.Add(CompareExact("statement_to", truth.GetProperty("statement_to").GetString(), stmt?.StatementTo));
            scores.Add(CompareMoney("opening_balance", truth.GetProperty("opening_balance").GetDecimal(), stmt?.OpeningBalance));
            scores.Add(CompareMoney("closing_balance", truth.GetProperty("closing_balance").GetDecimal(), stmt?.ClosingBalance));
            scores.Add(CompareMoney("total_credits", truth.GetProperty("total_credits").GetDecimal(), stmt?.TotalCredits));
        }

        return new DocumentEvaluationResult(documentId, truthDocType, scores);
    }

    public static EvaluationReport GenerateReport(IReadOnlyList<DocumentEvaluationResult> results)
    {
        var fieldTotals = new Dictionary<string, (int Correct, int Total)>();
        var typeTotals = new Dictionary<string, (int Correct, int Total)>();

        int grandTotal = 0;
        int grandCorrect = 0;

        foreach (var r in results)
        {
            if (!typeTotals.ContainsKey(r.DocumentType))
                typeTotals[r.DocumentType] = (0, 0);

            var currType = typeTotals[r.DocumentType];
            typeTotals[r.DocumentType] = (currType.Correct + r.CorrectFields, currType.Total + r.TotalFields);

            foreach (var f in r.FieldScores)
            {
                if (!fieldTotals.ContainsKey(f.FieldName))
                    fieldTotals[f.FieldName] = (0, 0);

                var currField = fieldTotals[f.FieldName];
                fieldTotals[f.FieldName] = (currField.Correct + (f.IsMatch ? 1 : 0), currField.Total + 1);

                grandTotal++;
                if (f.IsMatch) grandCorrect++;
            }
        }

        var fieldAcc = fieldTotals.ToDictionary(k => k.Key, v => v.Value.Total == 0 ? 0.0 : (double)v.Value.Correct / v.Value.Total);
        var typeAcc = typeTotals.ToDictionary(k => k.Key, v => v.Value.Total == 0 ? 0.0 : (double)v.Value.Correct / v.Value.Total);
        double overallAcc = grandTotal == 0 ? 0.0 : (double)grandCorrect / grandTotal;

        return new EvaluationReport(results, fieldAcc, typeAcc, overallAcc);
    }

    private static FieldScore CompareStrings(string fieldName, string? expected, string? actual)
    {
        if (expected is null && actual is null)
            return new FieldScore(fieldName, true, null, null);

        if (expected is null || actual is null)
            return new FieldScore(fieldName, false, expected, actual);

        var normExp = NormalizeString(expected);
        var normAct = NormalizeString(actual);
        bool match = string.Equals(normExp, normAct, StringComparison.OrdinalIgnoreCase);

        return new FieldScore(fieldName, match, expected, actual);
    }

    private static FieldScore CompareExact(string fieldName, string? expected, string? actual)
    {
        bool match = expected is not null && actual is not null && expected == actual;
        return new FieldScore(fieldName, match, expected, actual);
    }

    private static FieldScore CompareMoney(string fieldName, decimal? expected, decimal? actual)
    {
        if (!expected.HasValue || !actual.HasValue)
            return new FieldScore(fieldName, false, expected, actual);

        decimal expRound = Math.Round(expected.Value, 2, MidpointRounding.AwayFromZero);
        decimal actRound = Math.Round(actual.Value, 2, MidpointRounding.AwayFromZero);
        return new FieldScore(fieldName, expRound == actRound, expRound, actRound);
    }

    private static string NormalizeString(string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ");
}
