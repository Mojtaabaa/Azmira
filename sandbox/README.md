# Sandbox — preserved site settings

This folder holds **operator-owned** configuration that must survive binary updates.

| File | Role | Overwritten on update? |
|------|------|------------------------|
| `appsettings.json` | Live secrets & site settings (create from `.example`) | **Never** |
| `appsettings.defaults.json` | Canonical keys shipped with each release | Yes (safe) |
| `appsettings.json.example` | Documented sample | Yes (safe) |
| `PRESERVE.manifest.json` | Explicit preserve list for install scripts | Yes (safe) |
| `local/`, `secrets/` | Optional extra files you add | **Never** (manifest) |

## First install

1. Copy release to e.g. `C:\inetpub\Rotbeh`.
2. Copy `sandbox/appsettings.json.example` → `sandbox/appsettings.json` and fill secrets.
3. Start the app (or run `deploy/install-rotbeh.ps1`).

## Updates

Use [`deploy/install-rotbeh.ps1`](../deploy/install-rotbeh.ps1): it syncs binaries while keeping preserved sandbox files, then merges any **new** default keys into `appsettings.json` without replacing existing values.

Startup also runs the same merge so a manual file copy still picks up new settings keys.
