namespace PetProductivity.Shared.Models;

// Respuesta de login/registro/Google: el usuario + el JWT de sesión (60 min)
// + el refresh token rotatorio (T14-C0; clientes viejos ignoran el campo extra).
public class AuthResponse
{
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
