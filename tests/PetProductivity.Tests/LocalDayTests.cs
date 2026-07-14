using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>T8: el "hoy" local del usuario — bordes de medianoche y DST chileno.</summary>
public class LocalDayTests
{
    private static User Chile() => new() { TimeZoneId = "America/Santiago", Email = "x@x", Username = "x" };

    [Theory]
    // Invierno chileno (junio, UTC-4): medianoche local = 04:00 UTC.
    [InlineData("2026-06-15T03:59:00Z", "2026-06-14")] // 23:59 local → sigue siendo el 14
    [InlineData("2026-06-15T04:01:00Z", "2026-06-15")] // 00:01 local → ya es el 15
    // Verano chileno (enero, UTC-3): medianoche local = 03:00 UTC.
    [InlineData("2026-01-15T02:59:00Z", "2026-01-14")]
    [InlineData("2026-01-15T03:01:00Z", "2026-01-15")]
    public void TodayToken_CortaEnLaMedianocheLocal(string utcNow, string expectedDate)
    {
        var token = LocalDay.TodayTokenFor(Chile(), DateTime.Parse(utcNow).ToUniversalTime());
        Assert.Equal(DateTime.Parse(expectedDate).Date, token.Date);
        Assert.Equal(DateTimeKind.Utc, token.Kind); // Npgsql exige Utc
    }

    [Fact]
    public void StartOfTodayUtc_EsElInstanteRealDeLaMedianocheLocal()
    {
        // 15 jun 2026, 10:00 local Chile (UTC-4) → el día empezó a las 04:00 UTC.
        var start = LocalDay.StartOfTodayUtc(Chile(), DateTime.Parse("2026-06-15T14:00:00Z").ToUniversalTime());
        Assert.Equal(DateTime.Parse("2026-06-15T04:00:00Z").ToUniversalTime(), start);
    }

    [Fact]
    public void ZonaDesconocida_CaeAUtc_SinExcepcion()
    {
        var u = new User { TimeZoneId = "No/Existe", Email = "x@x", Username = "x" };
        var utc = DateTime.Parse("2026-06-15T03:59:00Z").ToUniversalTime();
        Assert.Equal(utc.Date, LocalDay.TodayTokenFor(u, utc).Date); // corta en medianoche UTC
    }

    [Fact]
    public void SinZona_UsaElDefaultChileno()
    {
        var u = new User { Email = "x@x", Username = "x" }; // TimeZoneId vacío
        var utc = DateTime.Parse("2026-06-15T03:59:00Z").ToUniversalTime();
        Assert.Equal(DateTime.Parse("2026-06-14").Date, LocalDay.TodayTokenFor(u, utc).Date);
    }
}
