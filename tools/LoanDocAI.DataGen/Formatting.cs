using System.Globalization;

namespace LoanDocAI.DataGen;

public static class Formatting
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Rounds to 2 places and forces scale 2 so JSON shows e.g. 45000.00.</summary>
    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero) + 0.00m;

    public static string Amount(decimal value, AmountStyle style)
    {
        var plain = value.ToString("0.00", Inv);
        return style switch
        {
            AmountStyle.RupeeSymbol => "₹" + IndianGroup(plain),
            AmountStyle.RsPrefix => "Rs. " + (value % 1 == 0 ? IndianGroup(plain)[..^3] : IndianGroup(plain)),
            AmountStyle.InrPlain => "INR " + plain,
            _ => IndianGroup(plain),
        };
    }

    /// <summary>Indian digit grouping (last three digits, then pairs): 125000.00 -> 1,25,000.00.</summary>
    private static string IndianGroup(string plain)
    {
        var dot = plain.IndexOf('.');
        var whole = plain[..dot];
        var frac = plain[dot..];
        if (whole.Length <= 3) return whole + frac;
        var head = whole[..^3];
        var tail = whole[^3..];
        var groups = new List<string>();
        while (head.Length > 2)
        {
            groups.Insert(0, head[^2..]);
            head = head[..^2];
        }
        if (head.Length > 0) groups.Insert(0, head);
        return string.Join(",", groups) + "," + tail + frac;
    }

    public static string Date(DateOnly date, DateStyle style) => style switch
    {
        DateStyle.DayMonAbbrev => date.ToString("dd-MMM-yyyy", Inv),
        DateStyle.SlashDayFirst => date.ToString("dd/MM/yyyy", Inv),
        _ => date.ToString("MMMM d, yyyy", Inv),
    };

    public static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", Inv);
}
