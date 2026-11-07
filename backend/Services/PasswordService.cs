using Microsoft.AspNetCore.Identity;

namespace KonkoorHelp.Api.Services;

public sealed class PasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly string _dummyHash;

    public PasswordService()
    {
        _dummyHash = _hasher.HashPassword(new object(), "not-a-real-password");
    }

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string password, string? hash)
    {
        var stored = string.IsNullOrEmpty(hash) ? _dummyHash : hash;
        var result = _hasher.VerifyHashedPassword(new object(), stored, password);
        return !string.IsNullOrEmpty(hash) && result != PasswordVerificationResult.Failed;
    }
}
