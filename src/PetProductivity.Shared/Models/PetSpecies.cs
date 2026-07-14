namespace PetProductivity.Shared.Models;

/// <summary>
/// Especies visuales de la mascota PERSONAL (neutra).
/// Se asigna aleatoriamente al nacer (server-authoritative) y es solo cosmética:
/// las 3 comparten exactamente los mismos stats. Exclusivas de mascotas personales
/// (las compartidas/grupo usarán otro set).
/// </summary>
public enum PetSpecies
{
    Sprout = 0, // verde
    Ember = 1,  // fuego
    Aqua = 2    // agua
}
