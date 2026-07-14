using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

public class PetEvolutionTests
{
    [Theory]
    // Umbrales T26: 50 / 600 / 2500 (Egg→Master ≈ 2-4 semanas a ~150 XP/día).
    [InlineData(0, EvolutionStage.Egg)]
    [InlineData(100, EvolutionStage.Baby)]
    [InlineData(600, EvolutionStage.Adult)]
    [InlineData(1500, EvolutionStage.Adult)]
    [InlineData(2500, EvolutionStage.Master)]
    public void EvolutionStage_DerivaDeTotalXp(double xp, EvolutionStage expected)
    {
        var pet = new Pet { TotalXp = xp };
        Assert.Equal(expected, pet.EvolutionStage);
    }

    [Fact]
    public void AddStatXp_SubeTotalXp_YEvoluciona()
    {
        var pet = new Pet { CurrentArchetype = Archetype.Neutral };
        Assert.Equal(EvolutionStage.Egg, pet.EvolutionStage);

        pet.AddStatXp("Mente", 100);

        Assert.Equal(100, pet.TotalXp);
        Assert.Equal(EvolutionStage.Baby, pet.EvolutionStage);
    }

    [Fact]
    public void Phoenix_CristalizaAlMorir_YReviveSoloConDificultad9oMas()
    {
        var pet = new Pet { CurrentArchetype = Archetype.Neutral };

        pet.ApplyDamage(100);
        Assert.Equal(PetStatus.Crystallized, pet.Status);
        Assert.Equal(0, pet.Health);

        // Tarea fácil no revive.
        Assert.False(pet.TryRevive(5));
        Assert.Equal(PetStatus.Crystallized, pet.Status);

        // Mega-tarea (≥9) revive, débil (20% HP).
        Assert.True(pet.TryRevive(9));
        Assert.Equal(PetStatus.Alive, pet.Status);
        Assert.Equal(20, pet.Health);
    }

    [Fact]
    public void AddStatXp_NoCreceMientrasEstaCristalizada()
    {
        var pet = new Pet { CurrentArchetype = Archetype.Neutral };
        pet.ApplyDamage(100); // cristaliza

        pet.AddStatXp("Mente", 100);

        Assert.Equal(0, pet.TotalXp); // los cristales no crecen
    }
}
