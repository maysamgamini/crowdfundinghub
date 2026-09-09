namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Durable storage for an ES256 signing key's private component (PKCS#8, base64), so the same
/// key survives an app restart instead of being regenerated (which would invalidate every
/// already-issued, unexpired token).
/// </summary>
public sealed class SigningKeyRecord
{
    private SigningKeyRecord()
    {
    }

    public SigningKeyRecord(string kid, string privateKeyPkcs8Base64, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        Kid = kid;
        PrivateKeyPkcs8Base64 = privateKeyPkcs8Base64;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Kid { get; private set; } = string.Empty;
    public string PrivateKeyPkcs8Base64 { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Retires this key as the one used to sign new tokens (TICKET-037's key rotation). The
    /// record itself is kept, not deleted — the public JWKS still needs it to verify tokens
    /// signed under this <see cref="Kid"/> before they expire.
    /// </summary>
    public void Deactivate() => IsActive = false;
}
