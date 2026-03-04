using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Budget.Api.Infrastructure.Persistence;
using Budget.Api.Modules.Budget.Contracts;
using Budget.Api.Modules.Budget.Domain;
using Microsoft.EntityFrameworkCore;

namespace Budget.Api.Modules.Budget.Services;

public sealed class AccountsService(
    BudgetDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IMarketPriceService marketPriceService,
    ILogger<AccountsService> logger)
    : BudgetServiceBase(dbContext, httpClientFactory, marketPriceService, logger),
      IAccountsService
{
    public async Task<AssetsOverviewResponse> GetAssetsOverviewAsync(int year, int month, CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        var generalSettings = await LoadUiStateAsync<GeneralSettingsState>(GeneralSettingsStateKey, cancellationToken);
        var baseCurrency = NormalizeCurrencyCode(generalSettings?.Currency);

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(x => x.IsArchived)
            .ThenBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var snapshots = await dbContext.AccountSnapshots
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.Year == year && x.Month == month)
            .OrderBy(x => x.Account.Name)
            .ToListAsync(cancellationToken);

        var transfers = await dbContext.AccountTransfers
            .AsNoTracking()
            .Include(x => x.FromAccount)
            .Include(x => x.ToAccount)
            .OrderByDescending(x => x.TransferDate)
            .Take(50)
            .ToListAsync(cancellationToken);

        var holdings = await dbContext.InvestmentHoldings
            .AsNoTracking()
            .Include(x => x.Account)
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        var savingsGoals = await dbContext.SavingsGoals
            .AsNoTracking()
            .Include(x => x.Account)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var accountResponses = accounts.Select(ToAccountResponse).ToArray();
        var accountById = accounts.ToDictionary(x => x.Id);
        var holdingResponses = holdings.Select(holding => ToHoldingResponse(holding)).ToArray();
        var investments = BuildInvestmentsDashboard(holdingResponses);
        var summary = await BuildAssetsSummaryAsync(baseCurrency, accounts, snapshots, cancellationToken);

        return new AssetsOverviewResponse(
            Year: year,
            Month: month,
            Summary: summary,
            Accounts: accountResponses,
            Transfers: transfers.Select(x => new AccountTransferResponse(
                x.Id,
                x.FromAccountId,
                x.FromAccount.Name,
                x.ToAccountId,
                x.ToAccount.Name,
                x.Amount,
                x.Note,
                x.TransferDate)).ToArray(),
            Snapshots: snapshots.Select(x => new AccountSnapshotResponse(
                x.AccountId,
                x.Account.Name,
                ToWireValue(x.Account.Kind),
                x.Year,
                x.Month,
                x.PlannedBalance,
                x.ActualBalance,
                x.UpdatedAt)).ToArray(),
            Holdings: holdingResponses,
            Investments: investments,
            SavingsGoals: savingsGoals.Select(x => ToSavingsGoalResponse(x, accountById)).ToArray());
    }

    public async Task<AssetsAccountsOverviewResponse> GetAccountsOverviewAsync(int year, int month, CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        var generalSettings = await LoadUiStateAsync<GeneralSettingsState>(GeneralSettingsStateKey, cancellationToken);
        var baseCurrency = NormalizeCurrencyCode(generalSettings?.Currency);

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(x => x.IsArchived)
            .ThenBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var snapshots = await dbContext.AccountSnapshots
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.Year == year && x.Month == month)
            .OrderBy(x => x.Account.Name)
            .ToListAsync(cancellationToken);

        var transfers = await dbContext.AccountTransfers
            .AsNoTracking()
            .Include(x => x.FromAccount)
            .Include(x => x.ToAccount)
            .OrderByDescending(x => x.TransferDate)
            .Take(50)
            .ToListAsync(cancellationToken);

        var savingsGoals = await dbContext.SavingsGoals
            .AsNoTracking()
            .Include(x => x.Account)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var accountResponses = accounts.Select(ToAccountResponse).ToArray();
        var accountById = accounts.ToDictionary(x => x.Id);
        var summary = await BuildAssetsSummaryAsync(baseCurrency, accounts, snapshots, cancellationToken);

        return new AssetsAccountsOverviewResponse(
            Year: year,
            Month: month,
            Summary: summary,
            Accounts: accountResponses,
            Transfers: transfers.Select(x => new AccountTransferResponse(
                x.Id,
                x.FromAccountId,
                x.FromAccount.Name,
                x.ToAccountId,
                x.ToAccount.Name,
                x.Amount,
                x.Note,
                x.TransferDate)).ToArray(),
            Snapshots: snapshots.Select(x => new AccountSnapshotResponse(
                x.AccountId,
                x.Account.Name,
                ToWireValue(x.Account.Kind),
                x.Year,
                x.Month,
                x.PlannedBalance,
                x.ActualBalance,
                x.UpdatedAt)).ToArray(),
            SavingsGoals: savingsGoals.Select(x => ToSavingsGoalResponse(x, accountById)).ToArray());
    }
    public async Task<BudgetAccountResponse> CreateAccountAsync(
        CreateBudgetAccountRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Account name is required.");
        }

        var kind = NormalizeAccountKind(request.Kind);
        var currency = NormalizeCurrencyCode(request.Currency);
        var now = DateTimeOffset.UtcNow;

        var account = new BudgetAccount
        {
            Name = request.Name.Trim(),
            Kind = kind,
            Currency = currency,
            CurrentBalance = decimal.Round(request.InitialBalance, 2, MidpointRounding.AwayFromZero),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Accounts.Add(account);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "BudgetAccount",
            EntityId = account.Id,
            EventType = "ACCOUNT_CREATED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                account.Name,
                kind = ToWireValue(account.Kind),
                account.Currency,
                account.CurrentBalance
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToAccountResponse(account);
    }

    public async Task<BudgetAccountResponse> UpdateAccountAsync(
        Guid accountId,
        UpdateBudgetAccountRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException("Account was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            account.Name = request.Name.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            account.Kind = NormalizeAccountKind(request.Kind);
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            account.Currency = NormalizeCurrencyCode(request.Currency);
            changed = true;
        }

        if (request.CurrentBalance.HasValue)
        {
            account.CurrentBalance = decimal.Round(request.CurrentBalance.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.IsArchived.HasValue)
        {
            account.IsArchived = request.IsArchived.Value;
            changed = true;
        }

        if (changed)
        {
            account.UpdatedAt = now;
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "BudgetAccount",
                EntityId = account.Id,
                EventType = "ACCOUNT_UPDATED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    account.Name,
                    kind = ToWireValue(account.Kind),
                    account.Currency,
                    account.CurrentBalance,
                    account.IsArchived
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToAccountResponse(account);
    }

    public async Task<AccountTransferResponse> CreateTransferAsync(
        CreateAccountTransferRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (request.FromAccountId == request.ToAccountId)
        {
            throw new ValidationException("Transfer must use two different accounts.");
        }

        if (request.Amount <= 0)
        {
            throw new ValidationException("Transfer amount must be greater than 0.");
        }

        var accountIds = new[] { request.FromAccountId, request.ToAccountId };
        var accounts = await dbContext.Accounts
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (!accounts.TryGetValue(request.FromAccountId, out var fromAccount) ||
            !accounts.TryGetValue(request.ToAccountId, out var toAccount))
        {
            throw new KeyNotFoundException("Transfer account was not found.");
        }

        if (fromAccount.IsArchived || toAccount.IsArchived)
        {
            throw new ValidationException("Cannot transfer from or to archived account.");
        }

        if (!string.Equals(fromAccount.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Transfers between different currencies are not supported.");
        }

        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        fromAccount.CurrentBalance = decimal.Round(fromAccount.CurrentBalance - amount, 2, MidpointRounding.AwayFromZero);
        toAccount.CurrentBalance = decimal.Round(toAccount.CurrentBalance + amount, 2, MidpointRounding.AwayFromZero);

        var now = DateTimeOffset.UtcNow;
        fromAccount.UpdatedAt = now;
        toAccount.UpdatedAt = now;

        var transfer = new AccountTransfer
        {
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            Amount = amount,
            Note = request.Note?.Trim() ?? string.Empty,
            TransferDate = request.TransferDate ?? now,
            CreatedAt = now
        };

        dbContext.AccountTransfers.Add(transfer);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "AccountTransfer",
            EntityId = transfer.Id,
            EventType = "TRANSFER_CREATED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                transfer.FromAccountId,
                transfer.ToAccountId,
                transfer.Amount,
                transfer.TransferDate,
                transfer.Note
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountTransferResponse(
            transfer.Id,
            transfer.FromAccountId,
            fromAccount.Name,
            transfer.ToAccountId,
            toAccount.Name,
            transfer.Amount,
            transfer.Note,
            transfer.TransferDate);
    }

    public async Task<IReadOnlyList<AccountSnapshotResponse>> UpsertAccountSnapshotsAsync(
        int year,
        int month,
        UpsertMonthlyAccountSnapshotsRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        ValidateYear(year);
        ValidateMonth(month);

        if (request.Snapshots.Count == 0)
        {
            return [];
        }

        var duplicateSnapshotAccountIds = request.Snapshots
            .GroupBy(x => x.AccountId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();
        if (duplicateSnapshotAccountIds.Length > 0)
        {
            throw new ValidationException(
                $"Duplicate account snapshots are not allowed. Duplicate account ids: {string.Join(", ", duplicateSnapshotAccountIds)}");
        }

        var accountIds = request.Snapshots.Select(x => x.AccountId).Distinct().ToArray();
        var accounts = await dbContext.Accounts
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var missing = accountIds.Where(x => !accounts.ContainsKey(x)).ToArray();
        if (missing.Length > 0)
        {
            throw new ValidationException($"Unknown account id(s): {string.Join(",", missing)}");
        }

        var existing = await dbContext.AccountSnapshots
            .Where(x => x.Year == year && x.Month == month && accountIds.Contains(x.AccountId))
            .ToDictionaryAsync(x => x.AccountId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var changed = 0;

        foreach (var input in request.Snapshots)
        {
            var planned = decimal.Round(input.PlannedBalance, 2, MidpointRounding.AwayFromZero);
            var actual = input.ActualBalance.HasValue
                ? (decimal?)decimal.Round(input.ActualBalance.Value, 2, MidpointRounding.AwayFromZero)
                : null;

            if (existing.TryGetValue(input.AccountId, out var snapshot))
            {
                if (snapshot.PlannedBalance == planned && snapshot.ActualBalance == actual)
                {
                    continue;
                }

                snapshot.PlannedBalance = planned;
                snapshot.ActualBalance = actual;
                snapshot.UpdatedAt = now;
                changed++;
                continue;
            }

            dbContext.AccountSnapshots.Add(new AccountSnapshot
            {
                AccountId = input.AccountId,
                Year = year,
                Month = month,
                PlannedBalance = planned,
                ActualBalance = actual,
                UpdatedAt = now
            });
            changed++;
        }

        if (changed > 0)
        {
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "AccountSnapshot",
                EntityId = Guid.NewGuid(),
                EventType = "MONTHLY_SNAPSHOTS_UPSERTED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    year,
                    month,
                    changed
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = await dbContext.AccountSnapshots
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.Year == year && x.Month == month)
            .OrderBy(x => x.Account.Name)
            .Select(x => new AccountSnapshotResponse(
                x.AccountId,
                x.Account.Name,
                ToWireValue(x.Account.Kind),
                x.Year,
                x.Month,
                x.PlannedBalance,
                x.ActualBalance,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return result;
    }
    public async Task<SavingsGoalResponse> CreateSavingsGoalAsync(
        CreateSavingsGoalRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Savings goal name is required.");
        }

        if (request.TargetAmount <= 0)
        {
            throw new ValidationException("Target amount must be greater than 0.");
        }

        ValidateSavingsGoalDate(request.TargetYear, request.TargetMonth);

        BudgetAccount? linkedAccount = null;
        if (request.AccountId.HasValue)
        {
            linkedAccount = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId.Value, cancellationToken);
            if (linkedAccount is null)
            {
                throw new KeyNotFoundException("Linked account was not found.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var goal = new SavingsGoal
        {
            Name = request.Name.Trim(),
            AccountId = request.AccountId,
            TargetAmount = decimal.Round(request.TargetAmount, 2, MidpointRounding.AwayFromZero),
            CurrentAmount = decimal.Round(request.CurrentAmount, 2, MidpointRounding.AwayFromZero),
            MonthlyContributionTarget = decimal.Round(request.MonthlyContributionTarget, 2, MidpointRounding.AwayFromZero),
            TargetYear = request.TargetYear,
            TargetMonth = request.TargetMonth,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.SavingsGoals.Add(goal);
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = "SavingsGoal",
            EntityId = goal.Id,
            EventType = "GOAL_CREATED",
            ChangedBy = actor,
            ChangedAt = now,
            Payload = JsonSerializer.Serialize(new
            {
                goal.Name,
                goal.TargetAmount,
                goal.MonthlyContributionTarget
            })
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        goal.Account = linkedAccount;

        return ToSavingsGoalResponse(
            goal,
            linkedAccount is null ? new Dictionary<Guid, BudgetAccount>() : new Dictionary<Guid, BudgetAccount> { [linkedAccount.Id] = linkedAccount });
    }

    public async Task<SavingsGoalResponse> UpdateSavingsGoalAsync(
        Guid goalId,
        UpdateSavingsGoalRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var goal = await dbContext.SavingsGoals
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Id == goalId, cancellationToken);

        if (goal is null)
        {
            throw new KeyNotFoundException("Savings goal was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            goal.Name = request.Name.Trim();
            changed = true;
        }

        if (request.ClearAccountLink)
        {
            goal.AccountId = null;
            goal.Account = null;
            changed = true;
        }
        else if (request.AccountId.HasValue)
        {
            var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId.Value, cancellationToken);
            if (account is null)
            {
                throw new KeyNotFoundException("Linked account was not found.");
            }

            goal.AccountId = account.Id;
            goal.Account = account;
            changed = true;
        }

        if (request.TargetAmount.HasValue)
        {
            if (request.TargetAmount <= 0)
            {
                throw new ValidationException("Target amount must be greater than 0.");
            }

            goal.TargetAmount = decimal.Round(request.TargetAmount.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.CurrentAmount.HasValue)
        {
            goal.CurrentAmount = decimal.Round(request.CurrentAmount.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.MonthlyContributionTarget.HasValue)
        {
            goal.MonthlyContributionTarget = decimal.Round(request.MonthlyContributionTarget.Value, 2, MidpointRounding.AwayFromZero);
            changed = true;
        }

        if (request.ClearTargetDate)
        {
            goal.TargetYear = null;
            goal.TargetMonth = null;
            changed = true;
        }
        else if (request.TargetYear.HasValue || request.TargetMonth.HasValue)
        {
            var targetYear = request.TargetYear ?? goal.TargetYear;
            var targetMonth = request.TargetMonth ?? goal.TargetMonth;
            ValidateSavingsGoalDate(targetYear, targetMonth);
            goal.TargetYear = targetYear;
            goal.TargetMonth = targetMonth;
            changed = true;
        }

        if (changed)
        {
            goal.UpdatedAt = now;
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = "SavingsGoal",
                EntityId = goal.Id,
                EventType = "GOAL_UPDATED",
                ChangedBy = actor,
                ChangedAt = now,
                Payload = JsonSerializer.Serialize(new
                {
                    goal.Name,
                    goal.TargetAmount,
                    goal.CurrentAmount,
                    goal.MonthlyContributionTarget,
                    goal.TargetYear,
                    goal.TargetMonth
                })
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        Dictionary<Guid, BudgetAccount> accountMap = [];
        if (goal.Account is not null)
        {
            accountMap[goal.Account.Id] = goal.Account;
        }

        return ToSavingsGoalResponse(goal, accountMap);
    }
}
