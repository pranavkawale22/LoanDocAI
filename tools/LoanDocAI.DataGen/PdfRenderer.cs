using System.Globalization;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static LoanDocAI.DataGen.Formatting;

namespace LoanDocAI.DataGen;

/// <summary>Renders generated documents as text-based PDFs. Three layouts per document type.</summary>
public static class PdfRenderer
{
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Noto Sans (SIL OFL, see Fonts/OFL.txt) is embedded because QuestPDF's default Lato has no U+20B9 rupee glyph.
    private const string FontFamily = "Noto Sans";

    static PdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var assembly = typeof(PdfRenderer).Assembly;
        foreach (var name in new[] { "Fonts.NotoSans-Regular.ttf", "Fonts.NotoSans-Bold.ttf" })
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded font {name} is missing.");
            FontManager.RegisterFontFromStream(stream);
        }
    }

    public static byte[] Render(GeneratedDocument doc) => doc switch
    {
        SalarySlipDocument s => Build(c => Slip(c, s)),
        BankStatementDocument b => Build(c => Statement(c, b)),
        _ => throw new ArgumentException($"Unknown document type {doc.GetType().Name}", nameof(doc)),
    };

    private static byte[] Build(Action<ColumnDescriptor> content) =>
        Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(8.5f).FontFamily(FontFamily));
                page.Content().Column(content);
            }))
            .WithMetadata(new DocumentMetadata { CreationDate = FixedTimestamp, ModifiedDate = FixedTimestamp })
            .GeneratePdf();

    // ---- salary slips -------------------------------------------------------------------------

    private static void Slip(ColumnDescriptor col, SalarySlipDocument d)
    {
        string Amt(decimal v) => Amount(v, d.AmountStyle);
        var f = d.Fields;
        var gross = d.Earnings.Sum(e => e.Amount);
        var totalDeductions = d.Deductions.Sum(x => x.Amount);
        col.Spacing(8);

        switch (d.Layout)
        {
            case 0:
                Title(col, f.EmployerName, 16);
                Line(col, $"Salary Slip for {d.PayDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}");
                Line(col, "Employee Name", f.ApplicantName);
                Line(col, "Employee ID", d.EmployeeId);
                Line(col, "Pay Date", Date(d.PayDate, d.DateStyle));
                var rows = new List<string[]>();
                for (var i = 0; i < Math.Max(d.Earnings.Count, d.Deductions.Count); i++)
                    rows.Add([
                        i < d.Earnings.Count ? d.Earnings[i].Label : "", i < d.Earnings.Count ? Amt(d.Earnings[i].Amount) : "",
                        i < d.Deductions.Count ? d.Deductions[i].Label : "", i < d.Deductions.Count ? Amt(d.Deductions[i].Amount) : "",
                    ]);
                rows.Add(["Gross Earnings", Amt(gross), "Total Deductions", Amt(totalDeductions)]);
                Grid(col, ["Earnings", "Amount", "Deductions", "Amount"], [false, true, false, true], [3, 2, 3, 2], rows, 1);
                Line(col, "Net Pay", Amt(f.NetSalary), bold: true);
                break;

            case 1:
                Title(col, "PAYSLIP", 18);
                Line(col, f.EmployerName.ToUpperInvariant());
                Line(col, "Name of Employee", f.ApplicantName.ToUpperInvariant());
                Line(col, "Emp Code", d.EmployeeId);
                Line(col, "Pay Month", d.PayDate.ToString("MM/yyyy", CultureInfo.InvariantCulture));
                Line(col, "Date of Payment", Date(d.PayDate, d.DateStyle));
                Grid(col, ["Earnings", "Amount (INR)"], [false, true], [4, 2],
                    d.Earnings.Select(e => new[] { e.Label, Amt(e.Amount) }).Append(["Total Earnings", Amt(gross)]).ToList(), 1);
                Grid(col, ["Deductions", "Amount (INR)"], [false, true], [4, 2],
                    d.Deductions.Select(e => new[] { e.Label, Amt(e.Amount) }).Append(["Total Deductions", Amt(totalDeductions)]).ToList(), 1);
                Line(col, "Net Salary", Amt(f.NetSalary), bold: true);
                break;

            default:
                Title(col, f.EmployerName, 14);
                Line(col, $"Pay Slip - {d.PayDate.ToString("MMM-yyyy", CultureInfo.InvariantCulture)}");
                Line(col, "Name", f.ApplicantName);
                Line(col, "ID", d.EmployeeId);
                Line(col, "Paid On", Date(d.PayDate, d.DateStyle));
                var all = d.Earnings.Select(e => new[] { e.Label, "Earning", Amt(e.Amount) })
                    .Append(["Gross Pay", "", Amt(gross)])
                    .Concat(d.Deductions.Select(e => new[] { e.Label, "Deduction", Amt(e.Amount) }))
                    .Append(["Take Home", "", Amt(f.NetSalary)])
                    .ToList();
                Grid(col, ["Particulars", "Type", "Amount"], [false, false, true], [4, 2, 2], all, 0);
                break;
        }
    }

    // ---- bank statements ----------------------------------------------------------------------

    private static void Statement(ColumnDescriptor col, BankStatementDocument d)
    {
        string Amt(decimal v) => Amount(v, d.AmountStyle);
        string Blank(decimal v) => v == 0 ? "" : Amt(v);
        var f = d.Fields;
        var totalDebits = d.Transactions.Sum(t => t.Debit);
        col.Spacing(8);

        switch (d.Layout)
        {
            case 0:
                Title(col, f.BankName, 16);
                Line(col, "Statement of Account");
                Line(col, "Account Holder", f.ApplicantName);
                Line(col, "Account No", d.MaskedAccountNumber);
                Line(col, "Statement Period", $"{Date(f.StatementFrom, d.DateStyle)} to {Date(f.StatementTo, d.DateStyle)}");
                Grid(col, ["Date", "Description", "Debit", "Credit", "Balance"], [false, false, true, true, true], [4.3f, 10, 3.8f, 3.8f, 3.8f],
                    d.Transactions.Select(t => new[] { Date(t.Date, d.DateStyle), t.Description, Blank(t.Debit), Blank(t.Credit), Amt(t.Balance) }).ToList(), 0);
                Line(col, "Opening Balance", Amt(f.OpeningBalance));
                Line(col, "Total Debits", Amt(totalDebits));
                Line(col, "Total Credits", Amt(f.TotalCredits));
                Line(col, "Closing Balance", Amt(f.ClosingBalance), bold: true);
                break;

            case 1:
                Title(col, f.BankName.ToUpperInvariant(), 16);
                Line(col, "Account Statement");
                Line(col, "Customer Name", f.ApplicantName.ToUpperInvariant());
                Line(col, "A/c", d.MaskedAccountNumber);
                Line(col, "From", Date(f.StatementFrom, d.DateStyle) + "  To  " + Date(f.StatementTo, d.DateStyle));
                Line(col, "Opening Bal", Amt(f.OpeningBalance));
                Line(col, "Total Deposits", Amt(f.TotalCredits));
                Line(col, "Total Withdrawals", Amt(totalDebits));
                Line(col, "Closing Bal", Amt(f.ClosingBalance));
                Grid(col, ["Txn Date", "Particulars", "Withdrawals", "Deposits", "Balance"], [false, false, true, true, true], [4.3f, 10, 3.8f, 3.8f, 3.8f],
                    d.Transactions.Select(t => new[] { Date(t.Date, d.DateStyle), t.Description, Blank(t.Debit), Blank(t.Credit), Amt(t.Balance) }).ToList(), 0);
                break;

            default:
                Title(col, f.BankName, 14);
                Line(col, "Account Statement");
                Line(col, "A/c Holder", f.ApplicantName);
                Line(col, "Account", d.MaskedAccountNumber);
                Line(col, "Period", $"{Date(f.StatementFrom, d.DateStyle)} - {Date(f.StatementTo, d.DateStyle)}");
                var rows = new List<string[]> { new[] { "", "Opening Balance b/f", "", "", Amt(f.OpeningBalance) + " Cr" } };
                rows.AddRange(d.Transactions.Select(t => new[]
                {
                    Date(t.Date, d.DateStyle), t.Description,
                    Amt(t.Debit > 0 ? t.Debit : t.Credit), t.Debit > 0 ? "Dr" : "Cr", Amt(t.Balance) + " Cr",
                }));
                Grid(col, ["Date", "Details", "Amount", "Dr/Cr", "Balance"], [false, false, true, false, true], [4.3f, 10, 3.8f, 1.3f, 4.2f], rows, 0);
                Line(col, "Total Credit Amount", Amt(f.TotalCredits));
                Line(col, "Total Debit Amount", Amt(totalDebits));
                Line(col, "Closing Balance c/f", Amt(f.ClosingBalance), bold: true);
                break;
        }
    }

    // ---- shared pieces ------------------------------------------------------------------------

    private static void Title(ColumnDescriptor col, string text, float size) =>
        col.Item().AlignCenter().Text(text).FontSize(size).Bold();

    private static void Line(ColumnDescriptor col, string text) => col.Item().Text(text);

    private static void Line(ColumnDescriptor col, string label, string value, bool bold = false) =>
        col.Item().Text(t =>
        {
            var l = t.Span(label + ": ");
            var v = t.Span(value);
            if (bold) { l.Bold(); v.Bold(); } else { l.Bold(); }
        });

    private static void Grid(ColumnDescriptor col, string[] headers, bool[] right, float[] widths,
        IReadOnlyList<string[]> rows, int boldTail)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { foreach (var w in widths) c.RelativeColumn(w); });
            table.Header(h =>
            {
                for (var i = 0; i < headers.Length; i++)
                    Cell(h.Cell().Background(Colors.Grey.Lighten3), right[i]).Text(headers[i]).Bold();
            });
            for (var r = 0; r < rows.Count; r++)
                for (var i = 0; i < headers.Length; i++)
                {
                    var text = Cell(table.Cell(), right[i]).Text(rows[r][i]);
                    if (r >= rows.Count - boldTail) text.Bold();
                }
        });
    }

    private static IContainer Cell(IContainer c, bool alignRight)
    {
        c = c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3);
        return alignRight ? c.AlignRight() : c;
    }
}
