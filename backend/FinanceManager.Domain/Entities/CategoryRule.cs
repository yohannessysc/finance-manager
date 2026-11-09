namespace FinanceManager.Domain.Entities;

/// <summary>
/// Rule for auto-categorizing transactions based on merchant, description, or amount patterns
/// </summary>
public class CategoryRule
{
    /// <summary>Unique identifier for this rule</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who created this rule</summary>
    public Guid UserId { get; set; }

    /// <summary>Category to auto-assign when rule matches</summary>
    public Guid CategoryId { get; set; }

    /// <summary>Rule name (e.g., "Salary from Acme Corp")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Pattern to match in merchant name (case-insensitive)</summary>
    public string? MerchantPattern { get; set; }

    /// <summary>Pattern to match in description (case-insensitive)</summary>
    public string? DescriptionPattern { get; set; }

    /// <summary>Minimum amount to match (for filtering by amount range)</summary>
    public decimal? MinAmount { get; set; }

    /// <summary>Maximum amount to match</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>Transaction type to match: 1 = Income, 2 = Expense, 3 = Transfer, null = any</summary>
    public int? TransactionTypeId { get; set; }

    /// <summary>Priority for rule application (higher = applied first)</summary>
    public int Priority { get; set; } = 0;

    /// <summary>Whether this rule is active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Number of times this rule has successfully matched</summary>
    public int MatchCount { get; set; } = 0;

    /// <summary>When this rule was created</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When this rule was last updated</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
