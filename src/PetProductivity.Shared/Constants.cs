namespace PetProductivity.Shared;

public static class Constants
{
    // Producción: backend en Render. Para desarrollo local contra el emulador, usar
    // "http://10.0.2.2:5051" (alias del localhost del PC) vía Ajustes → Dirección del Servidor.
    public const string BaseUrl = "https://petproductivity.onrender.com";

    // Comprobante de foco: una foto verificada por Gemini Vision multiplica la recompensa (bonus opt-in).
    public const double PhotoBonusMultiplier = 2.0;
}
