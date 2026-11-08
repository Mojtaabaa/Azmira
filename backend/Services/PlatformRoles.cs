using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;

namespace KonkoorHelp.Api.Services;

/// <summary>Shared platform-admin role checks (PlatformRole ≥ 100).</summary>
public static class PlatformRoles
{
    public const byte Administrator = IntegrationAccess.PlatformAdministratorRole;

    public static bool IsAdministrator(byte platformRole) =>
        IntegrationAccess.IsPlatformAdministrator(platformRole);

    public static bool IsAdministrator(UserAccount? user) =>
        user is not null && IsAdministrator(user.PlatformRole);
}
