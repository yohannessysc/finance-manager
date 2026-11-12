namespace FinanceManager.Domain.Entities;

// One category line of a split transaction (e.g. a Costco receipt split
// between Groceries and Household). A split transaction has no category
// of its own and its lines add up to its amount.
public class TransactionSplit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TransactionId { get; set; }

    public Guid CategoryId { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }
}
