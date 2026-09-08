namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

/// <summary>
/// Hashes and verifies user passwords.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);

    /// <summary>
    /// A fixed, valid-format hash with no corresponding real password, verified against when no
    /// user was found for a login attempt. Running the same PBKDF2 cost either way keeps login
    /// response time independent of whether the email exists, closing the timing side-channel an
    /// attacker could otherwise use to enumerate registered accounts (improvement.md §2.8).
    /// </summary>
    string DummyHash { get; }
}
