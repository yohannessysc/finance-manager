using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceManager.Infrastructure.Services;

public sealed record OfxTransaction(
    string FitId,
    DateOnly Date,
    decimal Amount,
    string Name,
    string? Memo);

public sealed record OfxStatement(
    string? Currency,
    string? AccountId,
    IReadOnlyList<OfxTransaction> Transactions);

// Reads bank and credit card statements in OFX/QFX format. Handles both
// OFX 1.x (SGML, leaf tags left unclosed) and OFX 2.x (XML).
public static partial class OfxParser
{
    public static OfxStatement Parse(string content)
    {
        if (!content.Contains("<OFX", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("This file is not an OFX or QFX statement.");

        var transactions = new List<OfxTransaction>();
        foreach (Match block in TransactionBlock().Matches(content))
        {
            var body = block.Groups[1].Value;
            var fitId = Value(body, "FITID");
            var posted = Value(body, "DTPOSTED");
            var amount = Value(body, "TRNAMT");
            var name = Value(body, "NAME") ?? Value(body, "PAYEE");
            var memo = Value(body, "MEMO");

            if (string.IsNullOrEmpty(fitId) || posted is null || amount is null)
                throw new FormatException("A transaction in the file is missing its id, date or amount.");

            transactions.Add(new OfxTransaction(
                fitId,
                ParseDate(posted),
                ParseAmount(amount),
                (name ?? memo ?? "Imported transaction").Trim(),
                string.IsNullOrWhiteSpace(memo) ? null : memo.Trim()));
        }

        return new OfxStatement(
            Value(content, "CURDEF")?.ToUpperInvariant(),
            Value(content, "ACCTID"),
            transactions);
    }

    // The leaf value runs to the next tag or line break (SGML) or to its
    // closing tag (XML). Entities used by OFX are decoded.
    private static string? Value(string body, string tag)
    {
        var match = Regex.Match(
            body,
            $@"<{tag}>\s*([^<\r\n]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        if (!match.Success)
            return null;

        var value = match.Groups[1].Value.Trim();
        return value.Length == 0
            ? null
            : value
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&apos;", "'")
                .Replace("&amp;", "&");
    }

    // YYYYMMDD, optionally followed by a time and [offset:TZ]. Only the
    // calendar date is kept: statements post by day.
    private static DateOnly ParseDate(string value)
    {
        if (value.Length < 8 ||
            !DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new FormatException($"Unrecognized date \"{value}\" in the file.");
        }

        return date;
    }

    private static decimal ParseAmount(string value)
    {
        var normalized = value.Replace(" ", string.Empty);
        // Some banks write decimal commas (e.g. -12,50).
        if (normalized.Contains(',') && !normalized.Contains('.'))
            normalized = normalized.Replace(',', '.');

        if (!decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount))
        {
            throw new FormatException($"Unrecognized amount \"{value}\" in the file.");
        }

        return amount;
    }

    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TransactionBlock();
}
