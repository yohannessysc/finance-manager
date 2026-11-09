using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Interfaces;

/// <summary>
/// Service for managing auto-categorization rules
/// </summary>
public interface ICategoryRuleService
{
    /// <summary>
    /// Create a new categorization rule for a user
    /// </summary>
    Task<CategoryRule> CreateRuleAsync(
        Guid userId,
        Guid categoryId,
        string name,
        string? merchantPattern = null,
        string? descriptionPattern = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        int? transactionTypeId = null,
        int priority = 0);

    /// <summary>
    /// Get all active rules for a user
    /// </summary>
    Task<IEnumerable<CategoryRule>> GetUserRulesAsync(Guid userId, bool activeOnly = true);

    /// <summary>
    /// Apply all rules to a transaction and return matched category
    /// </summary>
    Task<(Guid? CategoryId, string? RuleName, int Confidence)> EvaluateTransactionAsync(
        Guid userId,
        string description,
        string? merchant,
        decimal amount,
        int? transactionType = null);

    /// <summary>
    /// Update a rule
    /// </summary>
    Task<bool> UpdateRuleAsync(Guid ruleId, Guid userId, Dictionary<string, object> updates);

    /// <summary>
    /// Delete a rule
    /// </summary>
    Task<bool> DeleteRuleAsync(Guid ruleId, Guid userId);

    /// <summary>
    /// Get a specific rule
    /// </summary>
    Task<CategoryRule?> GetRuleAsync(Guid ruleId, Guid userId);
}
