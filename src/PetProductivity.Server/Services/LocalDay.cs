using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

/// <summary>
/// El "hoy" del usuario (T8): TODOS los cortes de día del juego (ritual, rendimientos decrecientes,
/// rachas) pasan por aquí, usando su zona IANA persistida (<see cref="User.TimeZoneId"/>).
/// Los timestamps de almacenamiento SIGUEN en UTC — esto solo decide dónde cae la medianoche.
/// </summary>
public static class LocalDay
{
    public const string DefaultTimeZone = "America/Santiago"; // base de usuarios actual

    public static TimeZoneInfo ZoneFor(User u)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                string.IsNullOrWhiteSpace(u.TimeZoneId) ? DefaultTimeZone : u.TimeZoneId);
        }
        catch (Exception)
        {
            return TimeZoneInfo.Utc; // id corrupto/desconocido: mejor UTC que un 500
        }
    }

    /// <summary>Fecha local de "hoy" como token comparable/almacenable. Kind=Utc porque Npgsql
    /// exige Utc en timestamptz — es una ETIQUETA de día, no un instante (comparar solo con otros tokens).</summary>
    public static DateTime TodayTokenFor(User u, DateTime? utcNow = null)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, ZoneFor(u));
        return DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
    }

    /// <summary>Instante UTC real en que empezó el "hoy" local (para filtrar columnas UTC como CreatedAt).</summary>
    public static DateTime StartOfTodayUtc(User u, DateTime? utcNow = null)
    {
        var tz = ZoneFor(u);
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, tz).Date;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), tz);
    }
}
