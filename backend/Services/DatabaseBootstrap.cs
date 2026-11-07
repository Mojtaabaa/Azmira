using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Learning;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services;

public static class DatabaseBootstrap
{
    public static async Task ApplyAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workspaces = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        if (env.IsEnvironment("Testing"))
        {
            // In-memory / test hosts cannot apply SQL migrations; EnsureCreated is sufficient.
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await DefaultGamificationSeed.EnsureAsync(db, cancellationToken);
            await DefaultStoreSeed.EnsureAsync(db, cancellationToken);
            var srsTesting = scope.ServiceProvider.GetRequiredService<SpacedRepetitionService>();
            await srsTesting.EnsureDefaultProfileAsync(cancellationToken);
            logger.LogInformation("Testing database EnsureCreated completed.");
            return;
        }

        await PrepareLegacyEnsureCreatedAsync(db, logger, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await workspaces.BootstrapAllAsync(cancellationToken);
        await DefaultGamificationSeed.EnsureAsync(db, cancellationToken);
        await DefaultStoreSeed.EnsureAsync(db, cancellationToken);
        var srs = scope.ServiceProvider.GetRequiredService<SpacedRepetitionService>();
        await srs.EnsureDefaultProfileAsync(cancellationToken);
        logger.LogInformation("Database migrated, workspaces and gamification defaults ensured.");
    }

    /// <summary>
    /// Databases created with EnsureCreated lack __EFMigrationsHistory.
    /// Stamp the initial migration when the legacy schema already exists so Migrate can apply additive changes.
    /// </summary>
    private static async Task PrepareLegacyEnsureCreatedAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken)) return;

        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (applied.Count > 0) return;

        var hasUsers = await TableExistsAsync(db, "Users", cancellationToken);
        if (!hasUsers) return;

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0) return;

        var initial = pending[0];
        logger.LogWarning(
            "Legacy EnsureCreated database detected. Stamping baseline migration {Migration} before applying remaining migrations.",
            initial);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
            BEGIN
                CREATE TABLE [__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
            END
            """,
            cancellationToken);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = {initial})
            BEGIN
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES ({initial}, {"10.0.6"});
            END
            """,
            cancellationToken);

        // EnsureCreated schema is missing workspace columns/tables; apply them via the next migration if any.
        // When only one migration exists (full snapshot), recreate missing pieces with EnsureCreated-compatible alter.
        if (pending.Count == 1)
        {
            await EnsureWorkspaceSchemaAsync(db, cancellationToken);
        }
    }

    private static async Task EnsureWorkspaceSchemaAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        // Additive columns on existing EnsureCreated tables.
        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('Users', 'DisplayName') IS NULL ALTER TABLE [Users] ADD [DisplayName] nvarchar(80) NULL;
            IF COL_LENGTH('Users', 'Locale') IS NULL ALTER TABLE [Users] ADD [Locale] nvarchar(10) NULL;
            IF COL_LENGTH('Users', 'PlatformRole') IS NULL ALTER TABLE [Users] ADD [PlatformRole] tinyint NOT NULL CONSTRAINT DF_Users_PlatformRole DEFAULT 0;
            IF COL_LENGTH('Users', 'LastWorkspaceId') IS NULL ALTER TABLE [Users] ADD [LastWorkspaceId] uniqueidentifier NULL;
            IF COL_LENGTH('Users', 'DeletedAt') IS NULL ALTER TABLE [Users] ADD [DeletedAt] datetime2 NULL;

            IF COL_LENGTH('StudyRecords', 'WorkspaceId') IS NULL ALTER TABLE [StudyRecords] ADD [WorkspaceId] uniqueidentifier NULL;
            IF COL_LENGTH('Notes', 'WorkspaceId') IS NULL ALTER TABLE [Notes] ADD [WorkspaceId] uniqueidentifier NULL;
            IF COL_LENGTH('NoteFolders', 'WorkspaceId') IS NULL ALTER TABLE [NoteFolders] ADD [WorkspaceId] uniqueidentifier NULL;
            IF COL_LENGTH('NoteTrash', 'WorkspaceId') IS NULL ALTER TABLE [NoteTrash] ADD [WorkspaceId] uniqueidentifier NULL;
            IF COL_LENGTH('NoteImages', 'WorkspaceId') IS NULL ALTER TABLE [NoteImages] ADD [WorkspaceId] uniqueidentifier NULL;
            IF COL_LENGTH('NoteSettings', 'WorkspaceId') IS NULL ALTER TABLE [NoteSettings] ADD [WorkspaceId] uniqueidentifier NULL;
            """,
            cancellationToken);

        // Create workspace tables if missing by syncing from the model without dropping existing tables.
        // Use a temporary context approach: raw CREATE matching AppDbContext.
        if (!await TableExistsAsync(db, "Workspaces", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE [Workspaces] (
                    [Id] uniqueidentifier NOT NULL,
                    [Type] tinyint NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [Slug] nvarchar(80) NULL,
                    [SchoolId] uniqueidentifier NULL,
                    [OwnerUserId] int NOT NULL,
                    [Status] tinyint NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    [DeletedAt] datetime2 NULL,
                    CONSTRAINT [PK_Workspaces] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Workspaces_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );
                CREATE INDEX [IX_Workspaces_OwnerUserId] ON [Workspaces] ([OwnerUserId]);
                CREATE UNIQUE INDEX [IX_Workspaces_OwnerUserId_Type] ON [Workspaces] ([OwnerUserId], [Type]) WHERE [Type] = 1 AND [DeletedAt] IS NULL;
                """,
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "WorkspaceMembers", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE [WorkspaceMembers] (
                    [Id] uniqueidentifier NOT NULL,
                    [WorkspaceId] uniqueidentifier NOT NULL,
                    [UserId] int NOT NULL,
                    [Role] tinyint NOT NULL,
                    [Status] tinyint NOT NULL,
                    [InvitedByUserId] int NULL,
                    [JoinedAt] datetime2 NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    [DeletedAt] datetime2 NULL,
                    CONSTRAINT [PK_WorkspaceMembers] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_WorkspaceMembers_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_WorkspaceMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_WorkspaceMembers_WorkspaceId_UserId] ON [WorkspaceMembers] ([WorkspaceId], [UserId]) WHERE [DeletedAt] IS NULL;
                """,
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "WorkspaceSettings", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE [WorkspaceSettings] (
                    [WorkspaceId] uniqueidentifier NOT NULL,
                    [Data] nvarchar(max) NOT NULL,
                    [LocaleDefault] nvarchar(10) NOT NULL,
                    [TimeZone] nvarchar(64) NOT NULL,
                    [FeatureFlags] nvarchar(max) NOT NULL,
                    [GamificationProfileId] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_WorkspaceSettings] PRIMARY KEY ([WorkspaceId]),
                    CONSTRAINT [FK_WorkspaceSettings_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE CASCADE
                );
                """,
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "WorkspaceSubscriptions", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE [WorkspaceSubscriptions] (
                    [Id] uniqueidentifier NOT NULL,
                    [WorkspaceId] uniqueidentifier NOT NULL,
                    [PlanCode] nvarchar(40) NOT NULL,
                    [Status] tinyint NOT NULL,
                    [SeatLimit] int NULL,
                    [StartsAt] datetime2 NULL,
                    [EndsAt] datetime2 NULL,
                    [ExternalProvider] nvarchar(40) NULL,
                    [ExternalReference] nvarchar(120) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_WorkspaceSubscriptions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_WorkspaceSubscriptions_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_WorkspaceSubscriptions_WorkspaceId] ON [WorkspaceSubscriptions] ([WorkspaceId]);
                """,
                cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudyRecords_WorkspaceId_Collection_Seq' AND object_id = OBJECT_ID('StudyRecords'))
                CREATE INDEX [IX_StudyRecords_WorkspaceId_Collection_Seq] ON [StudyRecords] ([WorkspaceId], [Collection], [Seq]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notes_WorkspaceId' AND object_id = OBJECT_ID('Notes'))
                CREATE INDEX [IX_Notes_WorkspaceId] ON [Notes] ([WorkspaceId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteFolders_WorkspaceId' AND object_id = OBJECT_ID('NoteFolders'))
                CREATE INDEX [IX_NoteFolders_WorkspaceId] ON [NoteFolders] ([WorkspaceId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteTrash_WorkspaceId' AND object_id = OBJECT_ID('NoteTrash'))
                CREATE INDEX [IX_NoteTrash_WorkspaceId] ON [NoteTrash] ([WorkspaceId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteImages_WorkspaceId' AND object_id = OBJECT_ID('NoteImages'))
                CREATE INDEX [IX_NoteImages_WorkspaceId] ON [NoteImages] ([WorkspaceId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteSettings_WorkspaceId' AND object_id = OBJECT_ID('NoteSettings'))
                CREATE INDEX [IX_NoteSettings_WorkspaceId] ON [NoteSettings] ([WorkspaceId]);
            """,
            cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableName, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @name
            ) THEN 1 ELSE 0 END
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }
}
