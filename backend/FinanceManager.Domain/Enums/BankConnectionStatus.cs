namespace FinanceManager.Domain.Enums;

public enum BankConnectionStatus
{
    /// <summary>Initial status, awaiting user authentication with bank</summary>
    Pending = 0,

    /// <summary>Successfully connected to bank</summary>
    Connected = 1,

    /// <summary>Connection expired or requires re-authentication</summary>
    ExpiredLink = 2,

    /// <summary>Connection manually disconnected by user</summary>
    Disconnected = 3,

    /// <summary>Connection failed due to error</summary>
    Failed = 4
}
