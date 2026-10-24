using System.Text.Json;
using System.Text.RegularExpressions;
using LoanDocAI.DataGen;

namespace LoanDocAI.Tests;

public class SyntheticDataTests
{
    private static readonly IReadOnlyList<GeneratedDocument> Docs = SyntheticDataGenerator.Generate();
    private static IEnumerable<SalarySlipDocument> Slips => Docs.OfType<SalarySlipDocument>();
    private static IEnumerable<BankStatementDocument> Statements => Docs.OfType<BankStatementDocument>();

    [Fact]
    public void Generates_ten_slips_and_ten_statements_with_expected_ids()
    {
        Assert.Equal(20, Docs.Count);
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"slip-{i:00}"), Slips.Select(s => s.Id));
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"stmt-{i:00}"), Statements.Select(s => s.Id));
    }

    [Fact]
    public void Salary_slips_are_arithmetically_consistent()
    {
        foreach (var s in Slips)
        {
            Assert.Equal(s.Earnings.Sum(e => e.Amount), s.Fields.GrossSalary);
            Assert.Equal(s.Fields.GrossSalary - s.Deductions.Sum(d => d.Amount), s.Fields.NetSalary);
            Assert.InRange(s.Fields.GrossSalary, 25000m, 250000m);
            Assert.True(s.Fields.NetSalary > 0);
            Assert.Equal(s.PayDate.ToString("yyyy-MM"), s.Fields.PayMonth);
        }
    }

    [Fact]
    public void Bank_statements_are_arithmetically_consistent()
    {
        foreach (var s in Statements)
        {
            Assert.InRange(s.Transactions.Count, 8, 15);
            var balance = s.Fields.OpeningBalance;
            foreach (var t in s.Transactions)
            {
                balance += t.Credit - t.Debit;
                Assert.Equal(balance, t.Balance);
                Assert.True(t.Balance >= 0);
                Assert.True(t.Debit == 0 ^ t.Credit == 0);
                Assert.InRange(t.Date, s.Fields.StatementFrom, s.Fields.StatementTo);
            }
            Assert.Equal(s.Transactions.Order(Comparer<Transaction>.Create((a, b) => a.Date.CompareTo(b.Date))), s.Transactions);
            Assert.Equal(s.Transactions.Sum(t => t.Credit), s.Fields.TotalCredits);
            Assert.Equal(s.Fields.OpeningBalance + s.Fields.TotalCredits - s.Transactions.Sum(t => t.Debit), s.Fields.ClosingBalance);
            Assert.Equal(balance, s.Fields.ClosingBalance);
            Assert.Single(s.Transactions, t => t.Description == s.SalaryCreditDescription);
        }
    }

    [Fact]
    public void Statement_periods_are_one_calendar_month_in_2026()
    {
        foreach (var s in Statements)
        {
            Assert.Equal(2026, s.Fields.StatementFrom.Year);
            Assert.Equal(1, s.Fields.StatementFrom.Day);
            Assert.Equal(s.Fields.StatementFrom.AddMonths(1).AddDays(-1), s.Fields.StatementTo);
        }
    }

    [Fact]
    public void Truth_json_has_exactly_the_spec_fields_for_each_type()
    {
        string[] slipKeys = ["document_type", "applicant_name", "employer_name", "pay_month", "gross_salary", "net_salary"];
        string[] stmtKeys = ["document_type", "applicant_name", "bank_name", "statement_from", "statement_to",
            "opening_balance", "closing_balance", "total_credits"];

        foreach (var doc in Docs)
        {
            using var json = JsonDocument.Parse(SyntheticDataGenerator.ToTruthJson(doc));
            var root = json.RootElement;
            var isSlip = doc is SalarySlipDocument;
            Assert.Equal(isSlip ? slipKeys : stmtKeys, root.EnumerateObject().Select(p => p.Name));
            Assert.Equal(isSlip ? "salary_slip" : "bank_statement", root.GetProperty("document_type").GetString());

            foreach (var p in root.EnumerateObject().Where(p => p.Name is "gross_salary" or "net_salary" or "opening_balance" or "closing_balance" or "total_credits"))
            {
                Assert.Equal(JsonValueKind.Number, p.Value.ValueKind);
                Assert.Matches(@"^\d+\.\d{2}$", p.Value.GetRawText());
            }
            foreach (var p in root.EnumerateObject().Where(p => p.Name is "statement_from" or "statement_to"))
                Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", p.Value.GetString()!);
            if (isSlip)
                Assert.Matches(@"^\d{4}-\d{2}$", root.GetProperty("pay_month").GetString()!);
        }
    }

    [Fact]
    public void Truth_json_excludes_account_number_and_employee_id()
    {
        foreach (var doc in Docs)
        {
            var json = SyntheticDataGenerator.ToTruthJson(doc);
            Assert.DoesNotContain("XXXXXX", json);
            Assert.DoesNotContain("EMP", json);
        }
    }

    [Fact]
    public void Generation_is_deterministic()
    {
        var again = SyntheticDataGenerator.Generate();
        Assert.Equal(Docs.Count, again.Count);
        for (var i = 0; i < Docs.Count; i++)
        {
            Assert.Equal(Serialize(Docs[i]), Serialize(again[i]));
            Assert.Equal(SyntheticDataGenerator.ToTruthJson(Docs[i]), SyntheticDataGenerator.ToTruthJson(again[i]));
        }
    }

    [Fact]
    public void Pdf_rendering_is_deterministic_and_produces_pdfs()
    {
        foreach (var doc in new[] { Docs[0], Docs[1], Docs[2], Docs[10], Docs[11], Docs[12] })
        {
            var first = PdfRenderer.Render(doc);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(first, 0, 4));
            Assert.Equal(first, PdfRenderer.Render(doc));
        }
    }

    private static string Serialize(GeneratedDocument doc) => JsonSerializer.Serialize(doc, doc.GetType());
}
