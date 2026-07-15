# T4 — Endgame: el juego se acaba en ~2 semanas

**Estado:** 🟡 opción E hecha (2026-07-03); **opción A (generaciones/prestigio) hecha como MVP (2026-07-15)** — falta solo su arte (estatua en el diorama, F4); B/C/D siguen pendientes · **Esfuerzo global:** L · **Depende de:** diorama F4 (arte), T1 (hitos), opcionalmente T7

> **A implementada (MVP, sin arte — 2026-07-15):** retirar al Maestro es una decisión del usuario (no se le quita nada). El Maestro pasa a una **vitrina de legado** (`User.RetiredPets`, columna JSON) y la MISMA entidad `Pet` renace como cría fresca Gen+1 (mismos umbrales → no hereda XP que salte etapas; conserva oro/inventario, que viven en `User`). **Server:** `AccountService.RetireAsync` (guardián Maestro = `EvolutionStage.Master`, criterio 4) + `POST /api/users/me/retire`; migración aditiva `AddPetGenerations` (`Pet.Generation` default 1 para filas existentes + `User.RetiredPets`). **Cliente:** botón "Retirar al Maestro" en Perfil (solo visible en Maestro) → confirmación → `DisplayPromptAsync` del nombre de la cría (decisión: prompt, no reusar la ceremonia; no hay endpoint de rename) → re-hidrata; insignia "Generación N" (solo si >1) + lista de legado. **Herencia:** solo prestigio (insignia + legado), **sin bono de XP** — no toca la economía (decisión del dueño; el +10%/gen queda como candidata en T29 para decidir con datos). Strings ES+EN. `PetGenerationTests` cubre criterios 3/4/5 (134 tests verdes). **Pendiente:** la **estatua colocable en el diorama** (criterio 3, mitad visual) espera el arte F4; hoy el ancestro se ve como lista en Perfil. Verificación en emulador/dueño pendiente.

> **E implementada (el fix barato del mismo síntoma, recomendado "primero"):** hoy Baby→Adult→Master ya no ocurre en silencio. **Server** (`PetService.ApplyRewardAsync`): captura la etapa antes/después de `AddStatXp`; si subió y es mascota personal, dispara un **push del hito** ("✨ ¡Moko evolucionó! Ahora es Adulto…") vía la política anti-spam de T2 (`NotificationPolicy`, tipo `evo_{n}` por etapa para que las 3 puedan avisar). **Cliente** (`DashboardViewModel`/`DashboardPage`): ceremonia in-place (overlay ✨ + nombre del hito + sprite + "toca para continuar") dirigida por `LastCelebratedStage` en `Preferences` comparada contra la etapa real (verdad del server) → dispara una sola vez, sobrevive a que maten la app (criterio 1), y la primera carga de una cuenta existente solo memoriza la etapa sin celebrar. Reutiliza el patrón del overlay de cristal — sin arte nuevo.
>
> **Verificado en emulador:** con la mascota en Cría, inyectar `LastCelebratedStage=0` y reabrir dispara la ceremonia (✨ + "Aqua · Cría" + "Toca para continuar"); tras mostrarse, el flag vuelve a 1 → no se repite. La detección server (comparación de `EvolutionStage`, ya cubierta por `PetEvolutionTests`) y el gating de push (`NotificationPolicy`, testeado) no requirieron test nuevo; 108 verdes.
>
> **Pendiente (necesita al dueño / arte F4):** **A** (generaciones/prestigio — el endgame verdadero, arte de estatuas), **C** (drops estacionales — infra lista, cuello = arte), **B** (niveles post-Master — arte ×3 especies), **D** (logros — capa sobre A). Todas son decisiones de diseño + producción de arte.

## El quiebre (por qué)

Master se alcanza a 501 XP; una tarea normal de dificultad 5 da ~50 XP, así que un usuario constante llega a Master en 10-15 días. Después, `TotalXp` sigue subiendo sin que **nada** cambie nunca más: no hay siguiente meta visible, ni prestigio, ni razón aspiracional para seguir. El bucle de recompensa se queda sin techo que perseguir justo cuando el usuario ya construyó el hábito — el momento exacto para dárselo más grande, no para quitárselo.

## Evidencia en el código

- `src/PetProductivity.Shared/Models/PetEvolution.cs:8-12` — Egg 0-50, Baby 51-200, Adult 201-500, Master 501+. Nada después.
- `src/PetProductivity.Server/Controllers/UsersController.cs:102` — se registra con `TotalXp = 50` (naces al borde de Baby): el techo real son ~450 XP de camino.
- `src/PetProductivity.Server/Services/PetService.cs:299-325` — `CheckEvolution` solo cambia arquetipo por stat dominante; el comentario `// Maybe trigger an event or notification here` (línea 322) delata que ni siquiera el hito actual se celebra.
- `src/PetProductivity.Server/Controllers/ShopController.cs:24-26` — el catálogo ya soporta ventanas `AvailableFrom/To` (F5.3): la infraestructura de contenido rotativo existe y está sin explotar.

## Opciones

### A. Generaciones / prestigio (retirar al Master)
Al llegar a Master (o cuando el usuario quiera, pasado el umbral), puede **retirar** a la mascota: se convierte en estatua/trofeo colocable en el diorama (el sistema `PlacedFurniture` ya existe) y nace un huevo nuevo — misma o nueva especie — con una herencia pequeña (p. ej. +10% XP o un cosmético heredado). Campo `Generation` en `Pet` + tabla o lista de "ancestros" (nombre, especie, fecha, stats finales).
- **Pros:** convierte el techo en un ciclo infinito; el diorama se llena de historia personal (cada estatua = un capítulo de tu vida productiva — nadie borra eso); la decisión de retirar es del usuario (no se le quita nada); reutiliza diorama + ceremonia de nacimiento ya construidos.
- **Contras:** el mayor esfuerzo de la lista: modelo de datos (ancestros), UI de retiro + ceremonia, arte de estatuas por especie; hay que decidir qué se hereda sin inflar el early game.
- **Esfuerzo:** L · **Toca:** shared + server + migración + cliente + arte.

### B. Subir el techo: niveles post-Master
Etapas nuevas (Elder/Mythic/…) o "niveles de Master" con umbrales crecientes (1 000, 2 500, 5 000…), cada uno con cambio visual (aura, corona — `MasterVisualChangeThreshold` en `PetEvolution.cs:27` ya apunta en esa dirección).
- **Pros:** extiende el sistema existente sin conceptos nuevos; solo constantes + sprites.
- **Contras:** patea el problema (todo techo finito se alcanza); la curva se estira y los hitos se espacian tanto que dejan de motivar; cada nivel nuevo exige arte para 3 especies.
- **Esfuerzo:** M · **Toca:** shared + cliente + arte.

### C. Contenido estacional rotativo (reutiliza `AvailableFrom/To`)
Drops mensuales/estacionales en el catálogo: muebles y estilos de sala por temporada ("colección invierno", Halloween), algunos solo comprables ese mes.
- **Pros:** infraestructura **ya hecha** (solo editar `info.json` del catálogo y reiniciar); da razón periódica de volver aunque la mascota esté al tope; escasez temporal = deseo; alimenta el diorama.
- **Contras:** es endgame de *coleccionista*, no de *progresión* — complementa pero no sustituye A/B; requiere producir arte con cadencia (el cuello real).
- **Esfuerzo:** S por drop (una vez hecho el arte) · **Toca:** catálogo + arte.

### D. Logros / medallas de largo plazo
Sistema de logros: "100 tareas", "1 000 min de foco", "racha 30", "revivir un Fénix", "3 generaciones"… con medalla en el Perfil y/o mueble-trofeo.
- **Pros:** metas de horizonte largo baratas de definir; muchos contadores ya existen (`TotalTasksCompleted`, `TotalFocusMinutes`, `MaxFocusStreak`); sinergia directa con T1 (hitos de racha son logros).
- **Contras:** los logros por sí solos retienen poco (son pasivos: nadie abre la app "para lograr"); mejor como capa sobre A/C que como plato principal.
- **Esfuerzo:** M · **Toca:** server (evaluador de logros) + cliente (vitrina) + arte ligero.

### E. Celebrar los hitos que ya existen (pre-requisito barato)
Antes de cualquier endgame: hoy Baby→Adult→Master ocurre **en silencio** (el sprite cambia y ya). Ceremonia de evolución (reutilizar el patrón de la ceremonia de nacimiento: flash + reveal + nombre del hito) + push "¡Moko evolucionó!".
- **Pros:** el pico de dopamina más alto del juego hoy está desperdiciado; esfuerzo pequeño con impacto inmediato; necesario igual para A y B.
- **Contras:** ninguno serio.
- **Esfuerzo:** S-M · **Toca:** cliente (+ push vía T2).

## Recomendación

**E primero** (celebrar lo que ya hay — es el fix barato del mismo síntoma), **C en paralelo** (drops estacionales: la infraestructura está lista y compra tiempo), y **A como el endgame verdadero** cuando el diorama F4 tenga su arte — las generaciones son la única opción que convierte el techo en ciclo y además le da al diorama su mejor razón de existir. **B** solo si A se descarta; **D** como capa encima de T1/A, nunca sola.

## Criterios de éxito / verificación

1. (E) Cruzar 200 XP dispara la ceremonia de evolución una sola vez; matar la app en medio no la pierde (flag persistido).
2. (C) Un ítem con `AvailableTo` vencido no aparece en catálogo ni se puede comprar (ya cubierto por `IsAvailable`, verificar en UI).
3. (A) Retirar un Master: aparece la estatua en inventario/diorama, nace huevo Gen 2 con la herencia definida, el ancestro queda consultable con sus stats finales.
4. (A) No se puede retirar antes de Master.
5. La progresión del huevo Gen 2 usa los mismos umbrales (no se hereda XP que salte etapas).

## Dependencias

- **Diorama F4** (arte) para estatuas y colecciones estacionales.
- **T2** para anunciar evoluciones y drops.
- **T1/T7** si los logros/hitos se integran (D).
