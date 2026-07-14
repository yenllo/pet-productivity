using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

public class SharedPetTests
{
    [Fact]
    public void UpdateAffection_ContribuirSube_IgnorarBaja_ConClamp()
    {
        var pet = new SharedPet();
        var u = Guid.NewGuid();

        // Nuevo usuario arranca neutro (50). Contribuir = +10.
        pet.UpdateAffection(u, contributed: true);
        Assert.Equal(60, pet.GetAffectionForUser(u));

        // No contribuir = -5.
        pet.UpdateAffection(u, contributed: false);
        Assert.Equal(55, pet.GetAffectionForUser(u));

        // Tope superior 100.
        for (int i = 0; i < 10; i++) pet.UpdateAffection(u, true);
        Assert.Equal(100, pet.GetAffectionForUser(u));

        // Tope inferior 0.
        for (int i = 0; i < 30; i++) pet.UpdateAffection(u, false);
        Assert.Equal(0, pet.GetAffectionForUser(u));
    }

    [Theory]
    [InlineData(80, PetMood.Happy)]
    [InlineData(50, PetMood.Neutral)]
    [InlineData(10, PetMood.Grumpy)]
    public void GetMoodForUser_SegunUmbrales(double affection, PetMood expected)
    {
        var pet = new SharedPet();
        var u = Guid.NewGuid();
        pet.UserAffection[u] = affection;
        Assert.Equal(expected, pet.GetMoodForUser(u));
    }

    [Fact]
    public void DecayAllAffection_BajaATodos_SinPasarseDeCero()
    {
        var pet = new SharedPet();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        pet.UserAffection[a] = 50;
        pet.UserAffection[b] = 1;

        pet.DecayAllAffection(2);

        Assert.Equal(48, pet.GetAffectionForUser(a));
        Assert.Equal(0, pet.GetAffectionForUser(b)); // clamp en 0
    }
}
