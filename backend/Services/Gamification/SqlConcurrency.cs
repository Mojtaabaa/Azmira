using KonkoorHelp.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

/// <summary>
/// Retries transient SQL concurrency failures (deadlock / serialization)
/// without enabling global EF EnableRetryOnFailure (which conflicts with
/// user-initiated transactions unless every call site uses an execution strategy).
/// </summary>
public static class SqlConcurrency
{
    public const int MaxAttempts = 4;

    public static bool IsTransient(Exception error)
    {
        for (var e = error; e is not null; e = e.InnerException!)
        {
            if (e is SqlException sql)
            {
                // 1205 deadlock; 3960/3961 snapshot/serializable conflict
                if (sql.Number is 1205 or 3960 or 3961) return true;
            }

            var message = e.Message;
            if (message.Contains("deadlock", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Serialization failure", StringComparison.OrdinalIgnoreCase)
                || message.Contains("was deadlocked", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static async Task RunAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                await Task.Delay(20 * attempt * attempt, cancellationToken);
            }
        }
    }

    public static async Task RunWithClearAsync(AppDbContext db, Func<Task> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < MaxAttempts)
            {
                db.ChangeTracker.Clear();
                await Task.Delay(20 * attempt * attempt, cancellationToken);
            }
        }
    }
}
