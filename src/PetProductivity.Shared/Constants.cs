namespace PetProductivity.Shared;

public static class Constants
{
    // Producción: backend en Heroku (migrado desde Render 2026-07-22). Para desarrollo local
    // contra el emulador, usar "http://10.0.2.2:5051" (alias del localhost del PC) vía
    // Ajustes → Dirección del Servidor.
    public const string BaseUrl = "https://pet-productivity-c03ac5654dd2.herokuapp.com";

    // Comprobante de foco: una foto verificada por Gemini Vision multiplica la recompensa (bonus opt-in).
    public const double PhotoBonusMultiplier = 2.0;

    // Toda sesión de foco ya está verificada por TIEMPO REAL medido por el server (a diferencia de un
    // texto libre, que el usuario puede inventar). Sin este piso, un foco corto (p. ej. 5 min → dificultad
    // mínima 1 por FocusMath) pagaba MENOS que escribir a mano una frase vaga ("leer") calificada 2 por el
    // juez — un incentivo perverso que premiaba mentir sobre trabajar. Se apila con PhotoBonusMultiplier.
    public const double FocusVerifiedMultiplier = 1.4;
}
