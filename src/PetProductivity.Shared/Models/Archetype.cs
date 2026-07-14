namespace PetProductivity.Shared.Models;

// 1. DEFINICIÓN DE LOS FOCOS (ARQUETIPOS)
public enum Archetype
{
    // Individuales
    Scholar,      // Erudito (Lógica, Memoria)
    Technologist, // Tecnólogo (Código, Arquitectura)
    Creator,      // Visionario (Arte, Creatividad)
    Athlete,      // Atleta (Fuerza, Salud)
    Executive,    // Estratega (Finanzas, Gestión)
    Neutral,      // Neutro (Cuerpo, Mente, Hogar, Bienestar) — mascota personal
    
    // Grupales
    Household,    // El Hogar (Mantenimiento)
    Guild         // El Gremio (Colaboración)
}
