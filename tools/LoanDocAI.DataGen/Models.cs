namespace LoanDocAI.DataGen;

public enum AmountStyle { RupeeSymbol, RsPrefix, InrPlain, BareLakh }

public enum DateStyle { DayMonAbbrev, SlashDayFirst, MonthNameDay }

public sealed record LineItem(string Label, decimal Amount);

public sealed record Transaction(DateOnly Date, string Description, decimal Debit, decimal Credit, decimal Balance);

/// <summary>Ground truth for a salary slip; serialized as snake_case JSON exactly as in SPEC.md.</summary>
public sealed record SalarySlipTruth(
    string DocumentType,
    string ApplicantName,
    string EmployerName,
    string PayMonth,
    decimal GrossSalary,
    decimal NetSalary);

/// <summary>Ground truth for a bank statement; serialized as snake_case JSON exactly as in SPEC.md.</summary>
public sealed record BankStatementTruth(
    string DocumentType,
    string ApplicantName,
    string BankName,
    DateOnly StatementFrom,
    DateOnly StatementTo,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalCredits);

public abstract record GeneratedDocument(string Id, int Layout, AmountStyle AmountStyle, DateStyle DateStyle)
{
    /// <summary>The ground-truth object written to data/synthetic/truth/&lt;id&gt;.json.</summary>
    public abstract object Truth { get; }
}

public sealed record SalarySlipDocument(
    string Id, int Layout, AmountStyle AmountStyle, DateStyle DateStyle,
    SalarySlipTruth Fields,
    string EmployeeId,
    DateOnly PayDate,
    IReadOnlyList<LineItem> Earnings,
    IReadOnlyList<LineItem> Deductions)
    : GeneratedDocument(Id, Layout, AmountStyle, DateStyle)
{
    public override object Truth => Fields;
}

public sealed record BankStatementDocument(
    string Id, int Layout, AmountStyle AmountStyle, DateStyle DateStyle,
    BankStatementTruth Fields,
    string MaskedAccountNumber,
    string SalaryCreditDescription,
    IReadOnlyList<Transaction> Transactions)
    : GeneratedDocument(Id, Layout, AmountStyle, DateStyle)
{
    public override object Truth => Fields;
}
