using System.Globalization;
using System.Text.Json;
using static LoanDocAI.DataGen.Formatting;

namespace LoanDocAI.DataGen;

/// <summary>
/// Builds 20 fictional loan documents in memory (10 salary slips, 10 bank statements).
/// All employers, banks, people and numbers are invented; output is fully determined by the fixed seed.
/// </summary>
public static class SyntheticDataGenerator
{
    public const int Seed = 42;
    private const int Year = 2026;

    private static readonly string[] FirstNames =
    [
        "Aarav", "Meera", "Karthik", "Sneha", "Rohan", "Divya", "Imran", "Pooja", "Suresh", "Anjali",
        "Vikram", "Lakshmi", "Farhan", "Nandini", "Harish", "Kavya", "Arjun", "Revathi", "Gurpreet", "Tanvi",
    ];

    private static readonly string[] LastNames =
    [
        "Kulkarni", "Iyer", "Nair", "Deshmukh", "Reddy", "Banerjee", "Qureshi", "Patil", "Menon", "Chaudhary",
        "Joshi", "Pillai", "Shaikh", "Rao", "Bhatt", "Gowda", "Mishra", "Sandhu", "Naidu", "Thakur",
    ];

    private static readonly string[] Employers =
    [
        "Brightwater Software Pvt Ltd", "Kaveri Agro Foods Pvt Ltd", "Nilgiri Logistics Pvt Ltd",
        "Orchid Textiles Ltd", "Meghdoot Analytics Pvt Ltd", "Sagarika Marine Exports Pvt Ltd",
        "Tarang Healthcare Services LLP", "Vindhya Steel Works Ltd", "Lakshya Learning Pvt Ltd",
        "Harsil Auto Components Pvt Ltd",
    ];

    private static readonly string[] Banks =
    [
        "Sahyadri Cooperative Bank", "Deccan Federal Bank", "Tungabhadra Rural Bank", "Himagiri Urban Bank",
        "Narmada Commercial Bank", "Konkan Savings Bank", "Malwa Mercantile Bank", "Ganga Valley Bank",
        "Aravali Cooperative Bank", "Utkal Federal Bank",
    ];

    private static readonly string[] DebitTemplates =
    [
        "UPI/DR/{ref}/Annapurna Grocers", "ATM WDL Cash", "House Rent Transfer", "Electricity Bill - Jyoti Discom",
        "Mobile Recharge - Tarang Telecom", "Home Loan EMI", "UPI/DR/{ref}/Fuel Point Services",
        "POS Chaitanya Pharmacy", "Insurance Premium - Suraksha Life", "UPI/DR/{ref}/Metro Smart Card",
        "Online Shopping - Bazaarwala",
    ];

    private static readonly string[] CreditTemplates =
    [
        "UPI/CR/{ref}/Family Transfer", "Interest Credit", "Refund - Bazaarwala", "UPI/CR/{ref}/Friend Repayment",
    ];

    private static readonly JsonSerializerOptions TruthJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        NewLine = "\n",
    };

    public static string ToTruthJson(GeneratedDocument doc) =>
        JsonSerializer.Serialize(doc.Truth, doc.Truth.GetType(), TruthJsonOptions) + "\n";

    /// <summary>Generates all 20 documents in memory. Two calls return identical content.</summary>
    public static IReadOnlyList<GeneratedDocument> Generate()
    {
        var rng = new Random(Seed);
        var firstNames = Shuffled(rng, FirstNames);
        var lastNames = Shuffled(rng, LastNames);
        var employers = Shuffled(rng, Employers);
        var banks = Shuffled(rng, Banks);
        string Person(int i) => $"{firstNames[i]} {lastNames[i]}";

        var docs = new List<GeneratedDocument>();
        for (var k = 0; k < 10; k++)
            docs.Add(NewSlip(rng, $"slip-{k + 1:00}", k, Person(k), employers[k]));
        for (var k = 0; k < 10; k++)
            docs.Add(NewStatement(rng, $"stmt-{k + 1:00}", k, Person(10 + k), banks[k], employers[(k + 3) % 10]));
        return docs;
    }

    // Layout, amount style and date style cycle so all 3 layouts and all 9 layout/date pairings appear.
    private static (int Layout, AmountStyle Amount, DateStyle Date) Styles(int k) =>
        (k % 3, (AmountStyle)(k % 4), (DateStyle)((k / 3 + k) % 3));

    private static SalarySlipDocument NewSlip(Random rng, string id, int k, string name, string employer)
    {
        var (layout, amountStyle, dateStyle) = Styles(k);
        var month = rng.Next(1, 10);
        var payDate = new DateOnly(Year, month, 1).AddMonths(1).AddDays(-1);

        var gross = Money(rng.Next(25000, 250000) + (rng.Next(10) < 3 ? rng.Next(1, 100) / 100m : 0m));
        var basic = Money(Math.Round(gross * 0.45m, 0));
        var hra = Money(Math.Round(basic * 0.40m, 0));
        var conveyance = gross >= 40000 && rng.Next(2) == 0 ? Money(1600) : 0.00m;
        var medical = gross >= 40000 && rng.Next(2) == 0 ? Money(1250) : 0.00m;
        var special = Money(gross - basic - hra - conveyance - medical);

        var earnings = new List<LineItem>
        {
            new(Pick(layout, "Basic", "Basic Pay", "BASIC SALARY"), basic),
            new(Pick(layout, "HRA", "House Rent Allowance", "H.R.A."), hra),
        };
        if (conveyance > 0) earnings.Add(new(Pick(layout, "Conveyance", "Conveyance Allowance", "Conv. Allowance"), conveyance));
        if (medical > 0) earnings.Add(new(Pick(layout, "Medical Allowance", "Medical Reimbursement", "Medical"), medical));
        earnings.Add(new(Pick(layout, "Special Allowance", "Other Allowance", "Special Allow."), special));

        var deductions = new List<LineItem>
        {
            new(Pick(layout, "PF", "Provident Fund (Employee)", "EPF"), Money(Math.Round(basic * 0.12m, 0))),
            new(Pick(layout, "Professional Tax", "Prof. Tax", "PT"), Money(200)),
        };
        if (gross > 50000)
            deductions.Add(new(Pick(layout, "TDS", "Income Tax (TDS)", "Tax Deducted at Source"),
                Money(gross * rng.Next(5, 16) / 100m)));

        var net = Money(gross - deductions.Sum(d => d.Amount));
        var employeeId = $"EMP{rng.Next(10000, 99999)}";

        var truth = new SalarySlipTruth("salary_slip", name, employer,
            payDate.ToString("yyyy-MM", CultureInfo.InvariantCulture), gross, net);
        return new SalarySlipDocument(id, layout, amountStyle, dateStyle, truth, employeeId, payDate, earnings, deductions);
    }

    private static BankStatementDocument NewStatement(Random rng, string id, int k, string name, string bank, string employer)
    {
        var (layout, amountStyle, dateStyle) = Styles(k);
        var from = new DateOnly(Year, rng.Next(1, 13), 1);
        var to = from.AddMonths(1).AddDays(-1);

        var opening = Money(rng.Next(5000, 400000) + rng.Next(0, 100) / 100m);
        var count = rng.Next(8, 16);
        var days = Enumerable.Range(0, count).Select(_ => rng.Next(1, to.Day + 1)).Order().ToArray();
        days[0] = rng.Next(1, Math.Min(days[1], 5) + 1); // salary lands in the first days, order preserved

        var upperEmployer = employer.ToUpperInvariant();
        var salaryDescription = layout switch
        {
            0 => $"SALARY CREDIT {upperEmployer}",
            1 => $"NEFT/SAL/{rng.Next(100000, 999999)}/{upperEmployer}",
            _ => $"By Salary - {employer}",
        };

        var transactions = new List<Transaction>();
        var balance = opening;
        for (var i = 0; i < count; i++)
        {
            decimal debit = 0.00m, credit = 0.00m;
            string description;
            if (i == 0)
            {
                credit = Money(rng.Next(25000, 200000) + rng.Next(0, 100) / 100m);
                description = salaryDescription;
            }
            else
            {
                var debitCeiling = Money(balance * 0.6m);
                if (rng.Next(5) > 0 && debitCeiling >= 150)
                {
                    var raw = Money(rng.Next(150, 30000) + rng.Next(0, 100) / 100m);
                    debit = Math.Min(raw, debitCeiling);
                    description = Ref(rng, DebitTemplates[rng.Next(DebitTemplates.Length)]);
                }
                else
                {
                    credit = Money(rng.Next(200, 15000) + rng.Next(0, 100) / 100m);
                    description = Ref(rng, CreditTemplates[rng.Next(CreditTemplates.Length)]);
                }
            }
            balance = Money(balance + credit - debit);
            transactions.Add(new Transaction(new DateOnly(Year, from.Month, days[i]), description, debit, credit, balance));
        }

        var totalCredits = Money(transactions.Sum(t => t.Credit));
        var truth = new BankStatementTruth("bank_statement", name, bank, from, to, opening, balance, totalCredits);
        var masked = $"XXXXXX{rng.Next(1000, 10000)}";
        return new BankStatementDocument(id, layout, amountStyle, dateStyle, truth, masked, salaryDescription, transactions);
    }

    private static string Pick(int layout, params string[] options) => options[layout];

    private static string Ref(Random rng, string template) =>
        template.Replace("{ref}", rng.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture));

    private static T[] Shuffled<T>(Random rng, T[] items)
    {
        var copy = items.ToArray();
        for (var i = copy.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }
}
