using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>T5: humor derivado de Hunger/Health/Status (sin estado nuevo) — prioridades y umbrales.</summary>
public class PetConditionTests
{
    [Fact]
    public void RecienNacida_EstaFeliz() // 100/100 → Happy
    {
        Assert.Equal(PetCondition.Happy, new Pet().Condition);
    }

    [Theory]
    [InlineData(50, PetCondition.Normal)]   // ni feliz (hambre ≤ 60) ni hambrienta (≥ 30)
    [InlineData(61, PetCondition.Happy)]    // justo sobre el umbral de feliz
    [InlineData(29, PetCondition.Hungry)]   // bajo Pet.HungryAt (mismo umbral que el push T2)
    [InlineData(30, PetCondition.Normal)]   // en el borde exacto NO está hambrienta
    public void Hambre_DefineElHumor(double hunger, PetCondition expected)
    {
        Assert.Equal(expected, new Pet { Hunger = hunger }.Condition);
    }

    [Fact]
    public void SaludBaja_EsDebil_AunqueTengaHambre() // débil gana a hambrienta
    {
        var p = new Pet { Hunger = 10 };
        p.ApplyDamage(70); // 100 → 30 < WeakAt
        Assert.Equal(PetCondition.Weak, p.Condition);
    }

    [Fact]
    public void Cristalizada_GanaATodo()
    {
        var p = new Pet { Hunger = 100 };
        p.ApplyDamage(200); // a 0 → cristaliza
        Assert.Equal(PetCondition.Crystal, p.Condition);
    }
}
