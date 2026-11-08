# Hotfix — Invalid column name `DeletedAt`

## Cause

Production DB was created with legacy `EnsureCreated` (or an incomplete stamp of `InitialWorkspace`). EF history may show migrations applied, but `Users.DeletedAt` (and related workspace soft-delete columns) were never added. Startup then fails in `BootstrapAllAsync` filtering `DeletedAt IS NULL`.

## Immediate SQL (run against production DB, then restart app)

```sql
IF COL_LENGTH('Users', 'DeletedAt') IS NULL ALTER TABLE [Users] ADD [DeletedAt] datetime2 NULL;
IF COL_LENGTH('Users', 'DisplayName') IS NULL ALTER TABLE [Users] ADD [DisplayName] nvarchar(80) NULL;
IF COL_LENGTH('Users', 'Locale') IS NULL ALTER TABLE [Users] ADD [Locale] nvarchar(10) NULL;
IF COL_LENGTH('Users', 'PlatformRole') IS NULL ALTER TABLE [Users] ADD [PlatformRole] tinyint NOT NULL CONSTRAINT DF_Users_PlatformRole DEFAULT 0;
IF COL_LENGTH('Users', 'LastWorkspaceId') IS NULL ALTER TABLE [Users] ADD [LastWorkspaceId] uniqueidentifier NULL;

IF COL_LENGTH('Workspaces', 'DeletedAt') IS NULL AND OBJECT_ID(N'[Workspaces]') IS NOT NULL
  ALTER TABLE [Workspaces] ADD [DeletedAt] datetime2 NULL;
IF COL_LENGTH('WorkspaceMembers', 'DeletedAt') IS NULL AND OBJECT_ID(N'[WorkspaceMembers]') IS NOT NULL
  ALTER TABLE [WorkspaceMembers] ADD [DeletedAt] datetime2 NULL;
```

## Code fix

`DatabaseBootstrap` now always runs idempotent `EnsureWorkspaceSchemaAsync` after `MigrateAsync` when `Users` exists, so stamped-but-incomplete schemas self-heal on next deploy.
