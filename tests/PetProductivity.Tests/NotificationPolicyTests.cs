using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>T2-D: la política anti-spam (quiet hours locales, 1 por tipo por día, opt-out).</summary>
public class NotificationPolicyTests
{
    // 15:00 en Chile invernal (UTC-4) = 19:00 UTC — hora "despierta" segura.
    private static readonly DateTime TardeUtc = DateTime.Parse("2026-06-15T19:00:00Z").ToUniversalTime();

    private static User Activo() => new()
    {
        Email = "x@x", Username = "x", NotificationsEnabled = true,
        DeviceToken = "tok", TimeZoneId = "America/Santiago"
    };

    [Fact]
    public void UsuarioActivo_DeTarde_Recibe()
        => Assert.True(NotificationPolicy.ShouldSend(Activo(), "hunger", TardeUtc));

    [Fact]
    public void OptOut_NoRecibe()
    {
        var u = Activo(); u.NotificationsEnabled = false;
        Assert.False(NotificationPolicy.ShouldSend(u, "hunger", TardeUtc));
    }

    [Fact]
    public void SinToken_NoRecibe()
    {
        var u = Activo(); u.DeviceToken = null;
        Assert.False(NotificationPolicy.ShouldSend(u, "hunger", TardeUtc));
    }

    [Theory]
    [InlineData("2026-06-15T03:30:00Z")] // 23:30 local → quiet
    [InlineData("2026-06-15T07:00:00Z")] // 03:00 local → quiet
    [InlineData("2026-06-15T11:30:00Z")] // 07:30 local → quiet
    public void QuietHours_Silencian(string utc)
        => Assert.False(NotificationPolicy.ShouldSend(Activo(), "hunger", DateTime.Parse(utc).ToUniversalTime()));

    [Fact]
    public void QuietHours_TerminanALas8Local()
        // 08:30 local invierno = 12:30 UTC → ya se puede.
        => Assert.True(NotificationPolicy.ShouldSend(Activo(), "hunger", DateTime.Parse("2026-06-15T12:30:00Z").ToUniversalTime()));

    [Fact]
    public void UnoPorTipoPorDia_YOtroTipoNoBloquea()
    {
        var u = Activo();
        NotificationPolicy.MarkSent(u, "hunger", TardeUtc);
        Assert.False(NotificationPolicy.ShouldSend(u, "hunger", TardeUtc.AddHours(2))); // mismo día: no
        Assert.True(NotificationPolicy.ShouldSend(u, "crystal", TardeUtc.AddHours(2))); // otro tipo: sí
        Assert.True(NotificationPolicy.ShouldSend(u, "hunger", TardeUtc.AddDays(1)));   // al día siguiente: sí
    }
}

/// <summary>T2: elegibilidad del aviso nocturno de racha.</summary>
public class StreakReminderTests
{
    // 21:00 local invierno chileno = 01:00 UTC del día siguiente.
    private static readonly DateTime Noche2100 = DateTime.Parse("2026-06-16T01:00:00Z").ToUniversalTime();

    private static User ConRacha(int dias, string ultimaActividad) => new()
    {
        Email = "x@x", Username = "x", NotificationsEnabled = true, DeviceToken = "tok",
        TimeZoneId = "America/Santiago", CurrentStreak = dias,
        LastActivityDate = DateTime.SpecifyKind(DateTime.Parse(ultimaActividad), DateTimeKind.Utc)
    };

    [Fact]
    public void AyerActivo_HoyNada_ALas21_Elegible()
        => Assert.True(StreakReminderHostedService.IsEligible(ConRacha(3, "2026-06-14"), Noche2100));

    [Fact]
    public void YaHizoAlgoHoy_NoElegible()
        => Assert.False(StreakReminderHostedService.IsEligible(ConRacha(3, "2026-06-15"), Noche2100));

    [Fact]
    public void RachaYaMuerta_NoElegible()
        => Assert.False(StreakReminderHostedService.IsEligible(ConRacha(3, "2026-06-12"), Noche2100));

    [Fact]
    public void MuyTemprano_NoElegible()
    {
        // 15:00 local (19:00 UTC del día 15): fuera de la ventana nocturna.
        var tarde = DateTime.Parse("2026-06-15T19:00:00Z").ToUniversalTime();
        Assert.False(StreakReminderHostedService.IsEligible(ConRacha(3, "2026-06-14"), tarde));
    }
}
