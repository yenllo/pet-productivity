# T24 — Revisión interna de la capa social y auth

**Estado:** ✅ hecho (2026-07-02) · **Tipo:** revisión · **Esfuerzo:** M · **Depende de:** T9

## Por qué esta revisión

`PetService` se leyó línea por línea, pero `GroupService` (277 líneas) y todo el flujo de auth (`AuthService`, `AuthController`, `TokenService`, el "reclamo" de cuenta invitada) solo se vieron de reojo. Son los flujos con más **estados intermedios** del sistema — justo donde se esconden los bugs que la lectura superficial no ve. Esta revisión es de *corrección lógica*, complementaria a la de seguridad (T22): aquí importa "¿hace lo correcto en los casos raros?", no "¿es explotable?".

## Qué revisar (checklist)

### Grupos (`GroupService.cs`)
1. **Unanimidad de solicitudes:** unión por código + aprobación unánime — ¿qué pasa si un miembro aprueba y otro sale del grupo antes de completar el voto? ¿El quórum se recalcula?
2. **Salir del grupo:** ¿qué le pasa a la mascota compartida cuando queda 1 miembro (vuelve a "dormida"), 0 miembros (¿se borra? ¿huérfana?)? ¿Y a las `TaskApprovals` pendientes del que se fue?
3. **Afecto anti-polizón + decaimiento:** `AffectionDecayHostedService` — ¿el afecto se recalcula bien al entrar/salir miembros? ¿Un miembro inactivo penaliza a la mascota de todos?
4. **Mascota de grupo dormida/huevo:** transiciones ≥2 miembros → despierta, voto unánime → nace. ¿Bordes al cruzar esos umbrales hacia atrás?

### Auth (`AuthService`, `AuthController`, `TokenService`)
5. **Expiración del token:** ¿los JWT expiran? Si sí, ¿el cliente detecta el 401 y re-autentica, o el usuario queda en un limbo silencioso? (no vi refresh en la lectura previa).
6. **Reclamo de cuenta invitada (`UpgradeAccount`):** ¿qué pasa si falla a mitad (email ya tomado, red cae)? ¿La cuenta invitada queda consistente o corrupta? ¿Se puede reclamar dos veces?
7. **OAuth con Google:** el flujo `/auth/google/start`+`/callback` → redirige a la app con `?token=`. ¿Qué pasa si el usuario ya tenía cuenta con ese email como invitado? ¿Se fusiona o se duplica?
8. **`ToggleRitualCell` con estado corrupto:** ya tiene parseo defensivo (`PetService.cs:334-337`); confirmar que cubre todos los casos legados.

## Cómo

- Lectura dirigida de `GroupService.cs`, `AuthService.cs`, `AuthController.cs` completos con estos escenarios en mente.
- Reproducir los flujos multi-estado en emulador (2 cuentas para grupos; invitado→reclamo; token expirado forzando un JWT corto en dev).

## Salida esperada

Lista de bordes rotos o dudosos, cada uno con: escenario, comportamiento actual, comportamiento esperado, y si merece fix inmediato o plan propio.

## Dependencias

- **T9** primero. Complementa **T22** (seguridad) desde el ángulo de corrección. #2 y #6 alimentan **T14** (borrado de cuenta). Solaparse con T22 en auth es deseable (dos ángulos del mismo código).

## Resultado (2026-07-02) — lectura dirigida completa + fixes con tests (56 verdes)

### Arreglado de inmediato (mismo commit)
1. **(#2) Voto fantasma en `TaskApprovals`:** al salir un miembro, su voto quedaba contando — en grupo de 3, el voto del que se fue podía completar la mayoría sin que nadie más votara. `LeaveGroupAsync` ahora limpia sus votos, borra sus tareas pendientes como solicitante, y al disolverse el grupo purga todas (antes quedaban huérfanas para siempre). Tests nuevos ×2.
2. **(#1/#4) Unanimidad pasiva atascada:** si el que faltaba por votar (nacimiento o solicitud de unión) se iba del grupo, la unanimidad quedaba lograda pero sin gatillo — atascada hasta un re-tap. `LeaveGroupAsync` ahora re-evalúa y completa (hatch directo; join vía `CompleteJoinAsync` extraído, respetando cupo). Tests nuevos ×2.
3. **(#1/#4) Carreras de voto en `ApproveJoinAsync`/`VoteToHatchAsync`:** mismo lost-update de T11-D2 (JSON last-write-wins) — serializados con `PetWriteLock` (por requestId / petId).
4. **(#6) Rama muerta en `UpgradeAccount`:** re-aplicar arquetipo si `TotalXp==0` era inalcanzable (toda mascota nace con 50 XP) — eliminada.

### Anotado (con tarea dueña / decisión pendiente)
5. **(#5) Token de 30 días sin refresh:** al expirar, un usuario de Google renace como **invitado nuevo en silencio** (el login Google borra SavedEmail/Password y el arranque cae al paso 3); un 401 a mitad de sesión no tiene manejo global (limbo hasta reiniciar). Ambos son exactamente lo que **T14-C0** ya planifica (token corto + refresh + revocación). Sin cambio ahora.
6. **(#5/T22) `SavedPassword` en `Preferences` en texto plano** (cliente) — ya rastreado como M4/M5 en T22 → T14-C0.
7. **(#7) Google con email ya registrado + invitado con progreso:** la sesión cambia a la cuenta existente y la mascota del invitado queda abandonada **sin aviso**. Correcto (no se pueden fusionar dos mascotas) pero merece un aviso → **T27** (UX).
8. **(#7) Fusión invitado→Google:** correcta — solo si el email no existe, guard `guest_` impide re-fusión, errores del flujo redirigen con `?error` sin tocar BD. ✓
9. **(#3) Afecto:** decay uniforme cada 12 h sobre todas las compartidas vivas; per-usuario (ánimo), no daña la salud común; init al entrar (50) y limpieza al salir. ✓
10. **(#4) Hallazgo de diseño → RESUELTO (2026-07-03):** las compartidas ahora **decaen colectivamente**: el dueño eligió "solo pasan hambre si TODO el grupo lleva inactivo". `DecayMath.ApplyGroupDecay` (no decae mientras cualquier miembro esté activo los últimos 3 días; usa la actividad más reciente del grupo como escudo) + loop en `HealthDecayHostedService` con push de cristalización a toda la familia. 3 tests nuevos.
11. **(#8) `ToggleRitualCell`:** parseo defensivo correcto (longitud 9 + TryParse + reset por fecha). ✓
12. **(#6) `UpgradeAccount`:** atómico (un solo SaveChanges; fallo a mitad no corrompe), email tomado → 400 sin cambios, re-upgrade = editar la propia cuenta (aceptable). ✓
