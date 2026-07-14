namespace PetProductivity.Server.Data;

// T14-C0: refresh token de sesión (rotatorio). Solo se guarda el HASH (SHA256) del token;
// el valor crudo viaja una vez al cliente y vive en su SecureStorage. Revocar = poner RevokedUtc.
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    public bool IsActive => RevokedUtc == null && DateTime.UtcNow < ExpiresUtc;
}
