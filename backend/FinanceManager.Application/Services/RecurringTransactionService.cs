
using FinanceManager.Application.DTOs.RecurringTransactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Services;

public class RecurringTransactionService : IRecurringTransactionService
{
    private readonly IRecurringTransactionRepository _repository;
    private readonly IFinancialTransactionRepository _transactionRepository;

    public RecurringTransactionService(
        IRecurringTransactionRepository repository,
        IFinancialTransactionRepository transactionRepository)
    {
        _repository = repository;
        _transactionRepository = transactionRepository;
    }

    public async Task<List<RecurringTransactionResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var schedules = await _repository.GetAllAsync(userId, ct);

        return schedules.Select(MapToResponse).ToList();
    }

    public async Task<RecurringTransactionResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var schedule = await _repository.GetByIdAsync(id, userId, ct);

        return schedule is null ? null : MapToResponse(schedule);
    }

    public async Task<RecurringTransactionResponse> CreateAsync(
        Guid userId,
        CreateRecurringTransactionRequest request,
        CancellationToken ct = default)
    {
        await ValidateAsync(
            userId,
            request.FinancialAccountId,
            request.DestinationAccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.Description,
            request.Frequency,
            request.StartDateUtc,
            request.EndDateUtc,
            request.TimeZoneId,
            ct);

        var nowUtc = DateTime.UtcNow;

        var nextOccurrenceUtc = CalculateNextOccurrence(
            request.StartDateUtc,
            request.Frequency,
            request.TimeZoneId,
            nowUtc);

        var schedule = new RecurringTransaction
        {
            UserId = userId,
            FinancialAccountId = request.FinancialAccountId,
            DestinationAccountId = request.DestinationAccountId,
            CategoryId = request.CategoryId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Frequency = request.Frequency,
            Status = RecurringStatus.Active,
            StartDateUtc = request.StartDateUtc,
            NextOccurrenceUtc = nextOccurrenceUtc,
            EndDateUtc = request.EndDateUtc,
            TimeZoneId = request.TimeZoneId,
            CreatedAtUtc = nowUtc
        };

        await _repository.AddAsync(schedule, ct);

        return MapToResponse(schedule);
    }

    public async Task<RecurringTransactionResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateRecurringTransactionRequest request,
        CancellationToken ct = default)
    {
        var schedule = await _repository.GetByIdAsync(id, userId, ct);

        if (schedule is null)
            return null;

        if (schedule.Status == RecurringStatus.Cancelled)
        {
            throw new ArgumentException(
                "Cancelled schedules cannot be edited.");
        }

        await ValidateAsync(
            userId,
            request.FinancialAccountId,
            request.DestinationAccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.Description,
            request.Frequency,
            request.StartDateUtc,
            request.EndDateUtc,
            request.TimeZoneId,
            ct);

        var nowUtc = DateTime.UtcNow;

        // Retrieve the latest occurrence already recorded
        // for this schedule and authenticated user.
        var lastPostedUtc =
            await _repository.GetLastPostedOccurrenceAsync(
                id,
                userId,
                ct);

        // Never schedule an occurrence at or before the
        // most recently posted occurrence.
        var cutoffUtc =
            lastPostedUtc.HasValue && lastPostedUtc.Value > nowUtc
                ? lastPostedUtc.Value
                : nowUtc;

        var nextOccurrenceUtc = CalculateNextOccurrence(
            request.StartDateUtc,
            request.Frequency,
            request.TimeZoneId,
            cutoffUtc);

        // Updates affect future occurrences.
        // Previously posted transactions remain unchanged.
        schedule.FinancialAccountId = request.FinancialAccountId;
        schedule.DestinationAccountId = request.DestinationAccountId;
        schedule.CategoryId = request.CategoryId;
        schedule.Type = request.Type;
        schedule.Amount = request.Amount;
        schedule.Description = request.Description.Trim();
        schedule.Frequency = request.Frequency;
        schedule.StartDateUtc = request.StartDateUtc;
        schedule.NextOccurrenceUtc = nextOccurrenceUtc;
        schedule.EndDateUtc = request.EndDateUtc;
        schedule.TimeZoneId = request.TimeZoneId;

        await _repository.SaveChangesAsync(ct);

        return MapToResponse(schedule);
    }

    public async Task<bool> PauseAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var schedule = await _repository.GetByIdAsync(id, userId, ct);

        if (schedule is null)
            return false;

        if (schedule.Status == RecurringStatus.Cancelled)
        {
            throw new ArgumentException(
                "Cancelled schedules cannot be paused.");
        }

        schedule.Status = RecurringStatus.Paused;

        await _repository.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> ResumeAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var schedule = await _repository.GetByIdAsync(id, userId, ct);

        if (schedule is null)
            return false;

        if (schedule.Status == RecurringStatus.Cancelled)
        {
            throw new ArgumentException(
                "Cancelled schedules cannot be resumed.");
        }

        var nowUtc = DateTime.UtcNow;

        // Retrieve the latest posted occurrence to ensure
        // resuming cannot return to an already posted date.
        var lastPostedUtc =
            await _repository.GetLastPostedOccurrenceAsync(
                id,
                userId,
                ct);

        var cutoffUtc =
            lastPostedUtc.HasValue && lastPostedUtc.Value > nowUtc
                ? lastPostedUtc.Value
                : nowUtc;

        // Skip payments missed while paused.
        if (schedule.NextOccurrenceUtc < cutoffUtc ||
            (lastPostedUtc.HasValue &&
             schedule.NextOccurrenceUtc <= lastPostedUtc.Value))
        {
            schedule.NextOccurrenceUtc = CalculateNextOccurrence(
                schedule.StartDateUtc,
                schedule.Frequency,
                schedule.TimeZoneId,
                cutoffUtc);
        }

        schedule.Status = RecurringStatus.Active;

        await _repository.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> CancelAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var schedule = await _repository.GetByIdAsync(id, userId, ct);

        if (schedule is null)
            return false;

        schedule.Status = RecurringStatus.Cancelled;

        await _repository.SaveChangesAsync(ct);

        return true;
    }

    private static DateTime CalculateNextOccurrence(
        DateTime startDateUtc,
        RecurrenceFrequency frequency,
        string timeZoneId,
        DateTime cutoffUtc)
    {
        if (startDateUtc > cutoffUtc)
            return startDateUtc;

        return RecurrenceCalculator.GetNextOccurrence(
            startDateUtc,
            frequency,
            cutoffUtc,
            timeZoneId);
    }

    private async Task ValidateAsync(
        Guid userId,
        Guid accountId,
        Guid? destinationAccountId,
        Guid? categoryId,
        TransactionType type,
        decimal amount,
        string? description,
        RecurrenceFrequency frequency,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        string timeZoneId,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException(
                "Invalid transaction type.");
        }

        if (!Enum.IsDefined(frequency))
        {
            throw new ArgumentException(
                "Invalid recurrence frequency.");
        }

        if (amount <= 0 || amount > 9999999999999999.99m)
        {
            throw new ArgumentException(
                "Amount must be positive and within the supported range.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                "Amount cannot have more than two decimal places.");
        }

        if (string.IsNullOrWhiteSpace(description) ||
            description.Trim().Length > 500)
        {
            throw new ArgumentException(
                "Description is required and cannot exceed 500 characters.");
        }

        if (startDateUtc.Kind != DateTimeKind.Utc ||
            (endDateUtc.HasValue &&
             endDateUtc.Value.Kind != DateTimeKind.Utc))
        {
            throw new ArgumentException(
                "Schedule dates must use UTC.");
        }

        if (endDateUtc.HasValue &&
            endDateUtc.Value < startDateUtc)
        {
            throw new ArgumentException(
                "End date cannot be earlier than the start date.");
        }

        if (string.IsNullOrWhiteSpace(timeZoneId) ||
            timeZoneId.Length > 100 ||
            timeZoneId != timeZoneId.Trim())
        {
            throw new ArgumentException(
                "A valid time zone is required.");
        }

        RecurrenceCalculator.GetTimeZone(timeZoneId);

        // Confirm the source account belongs to the user.
        var account = await _transactionRepository.GetAccountAsync(
            accountId,
            userId,
            ct);

        if (account is null)
        {
            throw new ArgumentException(
                "Invalid financial account.");
        }

        if (type == TransactionType.Transfer)
        {
            if (categoryId.HasValue)
            {
                throw new ArgumentException(
                    "Transfers cannot have categories.");
            }

            if (!destinationAccountId.HasValue ||
                destinationAccountId.Value == accountId)
            {
                throw new ArgumentException(
                    "A transfer requires a different destination account.");
            }

            var destination =
                await _transactionRepository.GetAccountAsync(
                    destinationAccountId.Value,
                    userId,
                    ct);

            if (destination is null)
            {
                throw new ArgumentException(
                    "Invalid destination account.");
            }

            if (destination.Currency != account.Currency)
            {
                throw new ArgumentException(
                    "Cross-currency transfers are not supported yet.");
            }
        }
        else
        {
            if (destinationAccountId.HasValue)
            {
                throw new ArgumentException(
                    "Only transfers can have a destination account.");
            }

            if (categoryId.HasValue)
            {
                var category =
                    await _transactionRepository.GetCategoryAsync(
                        categoryId.Value,
                        userId,
                        ct);

                if (category is null || category.Type != type)
                {
                    throw new ArgumentException(
                        "Invalid category for this transaction type.");
                }
            }
        }
    }

    private static RecurringTransactionResponse MapToResponse(
        RecurringTransaction schedule)
    {
        return new RecurringTransactionResponse(
            schedule.Id,
            schedule.FinancialAccountId,
            schedule.DestinationAccountId,
            schedule.CategoryId,
            schedule.Type,
            schedule.Amount,
            schedule.Description,
            schedule.Frequency,
            schedule.Status,
            schedule.StartDateUtc,
            schedule.NextOccurrenceUtc,
            schedule.EndDateUtc,
            schedule.CreatedAtUtc,
            schedule.TimeZoneId
        );
    }
}
