namespace PetProductivity.Shared.Models;

// T4-A: una mascota retirada al llegar a Maestro (prestigio/generaciones). Snapshot inmutable para
// la vitrina de legado en el Perfil. La estatua colocable en el diorama queda para el arte F4;
// aquí solo viven los datos (criterio 3: el ancestro consultable con sus stats finales).
public class RetiredPet
{
    public string Name { get; set; } = string.Empty;
    public PetSpecies Species { get; set; }
    public double FinalTotalXp { get; set; }
    public int Generation { get; set; }
    public DateTime RetiredAt { get; set; }
}
