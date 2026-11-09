using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

/// <summary>
/// Service for managing auto-categorization rules
/// </summary>
public class CategoryRuleService : ICategoryRuleService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ILogger<CategoryRuleService> _logger;

    public CategoryRuleService(
        FinanceDbContext dbContext,
        ILogger<CategoryRuleService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CategoryRule> CreateRuleAsync(
        Guid userId,
        Guid categoryId,
        string name,
        string? merchantPattern = null,
        string? descriptionPattern = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        int? transactionTypeId = null,
        int priority = 0)
    {
        // Verify category exists and belongs to user
        var category = await _dbContext.TransactionCategories
            .FirstOrDefaultAsync(tc => tc.Id == categoryId && tc.UserId == userId);

        if (category == null)
            throw new ArgumentException("Category not found or does not belong to user", nameof(categoryId));

        var rule = new CategoryRule
        {
            UserId = userId,
            CategoryId = categoryId,
            Name = name,
            MerchantPattern = merchantPattern,
            DescriptionPattern = descriptionPattern,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            TransactionTypeId = transactionTypeId,
            Priority = priority,
            IsActive = true
        };

        _dbContext.CategoryRules.Add(rule);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Created category rule {RuleId} for user {UserId}, category {CategoryId}",
            rule.Id,
            userId,
            categoryId);

        return rule;
    }

    public async Task<IEnumerable<CategoryRule>> GetUserRulesAsync(Guid userId, bool activeOnly = true)
    {
        var query = _dbContext.CategoryRules
            .Where(cr => cr.UserId == userId);

        if (activeOnly)
        {
            query = query.Where(cr => cr.IsActive);
        }

        return await query
            .OrderByDescending(cr => cr.Priority)
            .ThenBy(cr => cr.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<(Guid? CategoryId, string? RuleName, int Confidence)> EvaluateTransactionAsync(
        Guid userId,
        string description,
        string? merchant,
        decimal amount,
        int? transactionType = null)
    {
        var rules = await GetUserRulesAsync(userId, activeOnly: true);

        foreach (var rule in rules)
        {
            // Check transaction type
            if (rule.TransactionTypeId.HasValue && rule.TransactionTypeId != transactionType)
                continue;

            // Check amount range
            if (rule.MinAmount.HasValue && amount < rule.MinAmount)
                continue;

            if (rule.MaxAmount.HasValue && amount > rule.MaxAmount)
                continue;

            // Check merchant pattern
            if (!string.IsNullOrEmpty(rule.MerchantPattern))
            {
                if (string.IsNullOrEmpty(merchant) ||
                    !merchant.Contains(rule.MerchantPattern, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // Check description pattern
            if (!string.IsNullOrEmpty(rule.DescriptionPattern))
            {
                if (!description.Contains(rule.DescriptionPattern, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // All conditions matched!
            rule.MatchCount++;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Rule {RuleId} matched for user {UserId}, transaction '{Description}'",
                rule.Id,
                userId,
                description);

            // Confidence based on how many conditions matched
            int confidence = 100;
            int conditionsChecked = 0;

            if (rule.TransactionTypeId.HasValue) conditionsChecked++;
            if (rule.MinAmount.HasValue || rule.MaxAmount.HasValue) conditionsChecked++;
            if (!string.IsNullOrEmpty(rule.MerchantPattern)) conditionsChecked++;
            if (!string.IsNullOrEmpty(rule.DescriptionPattern)) conditionsChecked++;

            // High confidence if multiple specific conditions matched
            if (conditionsChecked == 0) confidence = 50; // Only priority matched
            if (conditionsChecked == 1) confidence = 75;
            if (conditionsChecked >= 2) confidence = 95;

            return (rule.CategoryId, rule.Name, confidence);
        }

        // No rules matched
        return (null, null, 0);
    }

    public async Task<bool> UpdateRuleAsync(Guid ruleId, Guid userId, Dictionary<string, object> updates)
    {
        var rule = await _dbContext.CategoryRules
            .FirstOrDefaultAsync(cr => cr.Id == ruleId && cr.UserId == userId);

        if (rule == null)
            return false;

        foreach (var kvp in updates)
        {
            switch (kvp.Key.ToLowerInvariant())
            {
                case "name":
                    rule.Name = kvp.Value?.ToString() ?? "";
                    break;
                case "merchantpattern":
                    rule.MerchantPattern = kvp.Value?.ToString();
                    break;
                case "descriptionpattern":
                    rule.DescriptionPattern = kvp.Value?.ToString();
                    break;
                case "minamount":
                    rule.MinAmount = kvp.Value is decimal minVal ? minVal : null;
                    break;
                case "maxamount":
                    rule.MaxAmount = kvp.Value is decimal maxVal ? maxVal : null;
                    break;
                case "transactiontypeid":
                    rule.TransactionTypeId = kvp.Value is int i ? i : null;
                    break;
                case "priority":
                    rule.Priority = Convert.ToInt32(kvp.Value);
                    break;
                case "isactive":
                    rule.IsActive = Convert.ToBoolean(kvp.Value);
                    break;
            }
        }

        rule.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated rule {RuleId} for user {UserId}", ruleId, userId);

        return true;
    }

    public async Task<bool> DeleteRuleAsync(Guid ruleId, Guid userId)
    {
        var rule = await _dbContext.CategoryRules
            .FirstOrDefaultAsync(cr => cr.Id == ruleId && cr.UserId == userId);

        if (rule == null)
            return false;

        _dbContext.CategoryRules.Remove(rule);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted rule {RuleId} for user {UserId}", ruleId, userId);

        return true;
    }

    public async Task<CategoryRule?> GetRuleAsync(Guid ruleId, Guid userId)
    {
        return await _dbContext.CategoryRules
            .FirstOrDefaultAsync(cr => cr.Id == ruleId && cr.UserId == userId);
    }
}
