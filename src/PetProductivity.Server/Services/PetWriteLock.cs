using System.Collections.Concurrent;

namespace PetProductivity.Server.Services;

/// <summary>
/// Serializa el read-modify-write sobre una MISMA mascota para evitar lost-updates cuando dos miembros
/// premian a la vez (o tarea + compra simultáneas).
/// ponytail: lock en memoria por petId; correcto para 1 instancia (Render free). Si algún día se escala a
/// varias instancias del server, migrar a token de concurrencia (xmin de Postgres) + reintento.
/// El diccionario crece hasta el nº de mascotas distintas premiadas (acotado y pequeño): no se purga.
/// </summary>
public class PetWriteLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(Guid petId)
    {
        var sem = _locks.GetOrAdd(petId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        return new Releaser(sem);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private bool _released;
        public Releaser(SemaphoreSlim sem) => _sem = sem;
        public void Dispose() { if (!_released) { _released = true; _sem.Release(); } }
    }
}
