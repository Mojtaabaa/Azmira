using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KonkoorHelp.Tests;

internal static class TestDbOptions
{
    /// <summary>
    /// EF InMemory ignores transactions; suppress the warning so production-style
    /// BeginTransactionAsync call sites remain testable.
    /// </summary>
    public static void UseInMemory(DbContextOptionsBuilder options, string databaseName) =>
        options.UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
}
