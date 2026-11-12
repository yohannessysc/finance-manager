namespace FinanceManager.Domain.Enums;

public enum BankTransactionStatus
{
    /// <summary>Transaction pending settlement</summary>
    Pending = 0,

    /// <summary>Transaction posted/settled</summary>
    Posted = 1,

    /// <summary>Transaction was reversed</summary>
    Reversed = 2,

    /// <summary>Transaction matched to local category</summary>
    Categorized = 3
}
