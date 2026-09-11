using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Analytics;
using TradeLedger.Core.Backtesting;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Domain.Backtesting;
using TradeLedger.Core.Persistence;

namespace TradeLedger.Api.Features.Backtests;

public static class BacktestAccountEndpoints
{
    public static void MapBacktestAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/backtests/accounts")
            .WithTags("Backtests")
            .RequireAuthorization();

        group.MapGet("/", async (
            TradeLedgerDbContext db,
            BacktestAccountService service,
            [FromQuery] bool? includeInactive,
            CancellationToken ct) =>
        {
            var query = db.BacktestAccounts.AsNoTracking();

            if (includeInactive != true)
            {
                query = query.Where(a => a.IsActive);
            }

            var accounts = await query.OrderBy(a => a.Name).ToListAsync(ct);
            var responses = new List<BacktestAccountResponse>(accounts.Count);

            foreach (var account in accounts)
            {
                var balance = await service.GetBalanceAsync(account.Id, ct);
                responses.Add(BacktestAccountResponse.From(account, balance));
            }

            return Results.Ok(responses);
        })
        .WithName("ListBacktestAccounts")
        .WithSummary("List backtest accounts")
        .WithDescription("Each account is a simulated balance that runs compound onto, so one strategy over thousands of trades reads as a single curve rather than a pile of unrelated runs. Backtest accounts are entirely separate from the real Account entity: nothing here can reach your live equity curve.")
        .Produces<List<BacktestAccountResponse>>();

        group.MapPost("/", async (
            [FromBody] CreateBacktestAccountRequest request,
            TradeLedgerDbContext db,
            BacktestAccountService service,
            CancellationToken ct) =>
        {
            var account = BacktestAccount.Create(
                request.Name,
                request.StartingBalance,
                request.Mode ?? BacktestAccountMode.Sequential,
                request.Description,
                request.Currency,
                request.BacktestStrategyId);

            db.BacktestAccounts.Add(account);
            await db.SaveChangesAsync(ct);

            var balance = await service.GetBalanceAsync(account.Id, ct);

            return Results.Created(
                $"/api/backtests/accounts/{account.Id}",
                BacktestAccountResponse.From(account, balance));
        })
        .WithName("CreateBacktestAccount")
        .WithSummary("Create a backtest account")
        .WithDescription("Sequential mode chains runs in date order so each opens at the previous one's closing balance and position sizing compounds; overlapping date ranges are then rejected, because compounding the same market twice is fiction. Independent mode starts every run at the starting balance and is just a folder for comparing variants.")
        .Produces<BacktestAccountResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", async (
            Guid id,
            TradeLedgerDbContext db,
            BacktestAccountService service,
            CancellationToken ct) =>
        {
            var account = await db.BacktestAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (account is null)
            {
                throw new ResourceNotFoundException("Backtest account", id);
            }

            var balance = await service.GetBalanceAsync(id, ct);

            return Results.Ok(BacktestAccountResponse.From(account, balance));
        })
        .WithName("GetBacktestAccount")
        .WithSummary("One backtest account")
        .WithDescription("Includes the current balance, which is the starting balance plus the net result of every succeeded run on a sequential account.")
        .Produces<BacktestAccountResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateBacktestAccountRequest request,
            TradeLedgerDbContext db,
            BacktestAccountService service,
            CancellationToken ct) =>
        {
            var account = await db.BacktestAccounts.FirstOrDefaultAsync(a => a.Id == id, ct)
                ?? throw new ResourceNotFoundException("Backtest account", id);

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                account.Rename(request.Name);
            }

            if (request.Description is not null)
            {
                account.Describe(request.Description);
            }

            if (request.IsActive is { } isActive)
            {
                account.SetActive(isActive);
            }

            if (request.Mode is { } mode)
            {
                account.SwitchMode(
                    mode,
                    await db.BacktestRuns.AnyAsync(r => r.BacktestAccountId == id, ct));
            }

            await db.SaveChangesAsync(ct);

            var balance = await service.GetBalanceAsync(id, ct);

            return Results.Ok(BacktestAccountResponse.From(account, balance));
        })
        .WithName("UpdateBacktestAccount")
        .WithSummary("Rename or reconfigure an account")
        .WithDescription("Switching an account with existing runs to sequential is refused: those runs may overlap in time and chaining them would double-count the same market.")
        .Produces<BacktestAccountResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            TradeLedgerDbContext db,
            [FromQuery] bool? confirm,
            CancellationToken ct) =>
        {
            var account = await db.BacktestAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);

            if (account is null)
            {
                throw new ResourceNotFoundException("Backtest account", id);
            }

            var runCount = await db.BacktestRuns.CountAsync(r => r.BacktestAccountId == id, ct);

            if (runCount > 0 && confirm != true)
            {
                throw new ResourceConflictException(
                    "account_still_holds_runs",
                    $"Deleting this account removes {runCount} run(s) and all their trades. Repeat with confirm=true.");
            }

            db.BacktestAccounts.Remove(account);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteBacktestAccount")
        .WithSummary("Delete an account and everything on it")
        .WithDescription("Cascades to every run, trade, execution and equity point on the account. Requires confirm=true once any run exists.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/equity-curve", async (
            Guid id,
            TradeLedgerDbContext db,
            BacktestAccountService service,
            CancellationToken ct) =>
        {
            if (!await db.BacktestAccounts.AnyAsync(a => a.Id == id, ct))
            {
                throw new ResourceNotFoundException("Backtest account", id);
            }

            return Results.Ok(await service.GetStitchedEquityCurveAsync(id, ct));
        })
        .WithName("GetBacktestAccountEquityCurve")
        .WithSummary("Stitched equity curve across every run")
        .WithDescription("Concatenates the per-trade equity points of every succeeded run in date order and measures drawdown over the whole history. Drawdown is computed by the same code as your live equity curve, so the two numbers mean the same thing.")
        .Produces<EquityCurve>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/summary", async (
            Guid id,
            TradeLedgerDbContext db,
            BacktestAccountService service,
            CancellationToken ct) =>
        {
            if (!await db.BacktestAccounts.AnyAsync(a => a.Id == id, ct))
            {
                throw new ResourceNotFoundException("Backtest account", id);
            }

            return Results.Ok(await service.GetSummaryAsync(id, ct));
        })
        .WithName("GetBacktestAccountSummary")
        .WithSummary("Performance across every run on the account")
        .WithDescription("Win rate, profit factor, expectancy and streaks over every simulated trade on the account, computed by the same metrics code as GET /api/analytics/summary. That is what makes a backtest comparable to your real trading rather than merely adjacent to it.")
        .Produces<PerformanceSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
