# T16 — Consolidar antes de ensanchar (+ unificar la voz/idioma)

**Estado:** ✅ hecho (2026-07-03) — 1a (política anotada en el índice) + 2a variante traducción-solo-display · **Esfuerzo global:** S-M · **Depende de:** —

> **Implementado:** **Parte 1 —** política escrita al tope de `00-indice.md`: ningún sistema nuevo mientras queden planes abiertos; toda idea nueva se anota como candidata; regla de idioma hacia adelante ("todo string que un usuario pueda leer nace en español"). **Parte 2 —** barrido: errores de controllers a ES (Shop "User/Pet not found", Tasks/Focus "Description is required", Users "Invalid credentials"/"Email already registered"); `TaskResult` "User or Pet not found" → ES; defaults "Egg"/"Pixel Egg" → "Huevo" (Pet, AuthRequest, invitado del cliente); **stats de arquetipos: claves internas en inglés intactas (BD + IA), el cliente traduce solo al mostrar** vía `PetVisuals.StatDisplayName` (22 stats mapeadas, aplicado en StatsPage). El XAML del cliente ya estaba 100% en ES (verificado por grep). Lo que queda en inglés es solo `DevController` (gateado a Development, no lo ve un usuario) y el sentinela interno "User not found" de `ToggleRitualCell` (el controller lo convierte en `NotFound()` sin cuerpo).
>
> **Verificado:** 101 tests verdes (incl. `AiJudgeFallbackTests` — las claves de stats no cambiaron, criterio 2); server y cliente compilan. El recorrido visual completo (criterio 1) se termina de auditar en **T27** con el dueño.

## El quiebre (por qué)

La superficie de la app ya es enorme para un solo desarrollador: grupos, foco, foco grupal, Vision, diorama, tienda, ritual, frenesí, Fénix, push, OAuth. Cada sistema nuevo añade mantenimiento permanente y reparte el pulido entre más frentes. El mayor riesgo de producto ahora no es que falte una feature: es seguir añadiendo sistemas antes de que los existentes estén profundos y coherentes. La incoherencia más visible de esa dispersión es el idioma: la app le habla al usuario mezclando español e inglés según qué subsistema conteste.

## Evidencia en el código

Idioma mezclado de cara al usuario:
- `src/PetProductivity.Server/Services/EmotionalSupportService.cs:68-74` — feedback y fallbacks en inglés ("Stay hydrated", "Keep building momentum") en el momento estrella del bucle. (Lo arregla T12 al fusionar la llamada.)
- Stats por arquetipo mezcladas: la mascota personal usa Cuerpo/Mente/Hogar/Bienestar (español) y los arquetipos de grupo Code/Endurance/Logic… (inglés) — `ArchetypeStats`, visibles en UI de stats y en la categoría que devuelve la IA (`AiJudgeService.cs:44` ya pide "if Spanish, return the exact Spanish word": el propio prompt convive con la mezcla).
- Mensajes de error de controllers mitad y mitad: "User not found." / "No tienes suficiente oro." (`ShopController.cs:50,74`), "Description is required" (`TasksController.cs:32`) — algunos llegan a UI.
- `Pet.Name` default "Egg" (`Pet.cs:15`), `EvolutionStage` en inglés si se muestra crudo.

Política (sin archivo:línea — es proceso):
- 16 planes abiertos en `tareas/` + F4 del diorama pendiente: la cola ya es larga; cada sistema nuevo la empuja hacia atrás.

## Opciones

### Parte 1 — Política de consolidación
**a. Congelar sistemas nuevos hasta vaciar la cola crítica (recomendada):** no se abre ningún sistema nuevo (mecánicas, pantallas mayores, integraciones) hasta cerrar al menos T8-T1-T2-T6 (engagement núcleo) y T9-T11 (fiabilidad). Las ideas nuevas se anotan como candidatas en `tareas/` en vez de implementarse — el costo de anotar es cero y mata el impulso de codearlas.
**b. Regla de intercambio:** por cada sistema nuevo, retirar o simplificar uno existente (¿se usa el semáforo? ¿el ×1.2 del ritual se entiende?). Más agresiva; útil si a falla.
**c. Sin política, confiar en el criterio:** es lo que hay hoy; la existencia de 16 planes sugiere que no basta.
- **Esfuerzo:** 0 en código; disciplina.

### Parte 2 — Unificación de idioma (tarea concreta)
**a. Español en todo lo que ve el usuario (recomendada):** barrido de strings de cara a UI — mensajes de `TaskResult`/errores de controllers que el cliente muestra, feedback (vía T12), y **traducir las stats de arquetipos de grupo** (Code→Código, Endurance→Resistencia…). Ojo: las stats son **claves de datos** (diccionario `Pet.Stats`, validadas en `ArchetypeStats.IsValidStatForArchetype`, juzgadas por la IA) — traducirlas exige mapear datos existentes en BD (migración de datos o aceptar claves legadas) y actualizar el prompt. La alternativa barata: claves internas quedan en inglés y el **cliente traduce solo para mostrar** (un dict estático en `PetVisuals`/converter) — cero migración, cero riesgo de datos.
**b. Localización formal (resx / i18n):** infraestructura de recursos multi-idioma.
- **Pros:** el camino "correcto" si algún día hay inglés.
- **Contras:** sobre-ingeniería hoy — un solo idioma objetivo, un solo dev; el costo se paga ahora y el beneficio es hipotético.
**c. Statu quo:** gratis, pero cada string nuevo hereda la ambigüedad de "¿en qué idioma va esto?".
- **Esfuerzo:** 2a con traducción-solo-display: S-M · **Toca:** cliente (display) + server (mensajes de error visibles) + prompt.

## Recomendación

**1a + 2a (variante traducción-solo-display).** La política cuesta cero y protege todo lo demás; el idioma se arregla sin tocar datos: claves en inglés por dentro, español por fuera, y una regla simple hacia adelante — *todo string que un usuario pueda leer nace en español*. 2b solo si algún día se decide mercado angloparlante (y entonces con resx de verdad).

## Criterios de éxito / verificación

1. Recorrido completo de la app (ceremonia → tarea → recompensa → tienda → grupo → foco → error de red) sin encontrar un solo string en inglés visible.
2. Las stats de un arquetipo de grupo se muestran en español y la IA sigue categorizando bien (las claves internas no cambiaron — test de regresión de `AiJudgeFallbackTests`).
3. `tareas/00-indice.md` es el único backlog: cualquier idea nueva de sistema aparece ahí como candidata antes de existir en código.

## Dependencias

- **T12** resuelve el string en inglés más importante (feedback).
- La política (Parte 1) gobierna el orden de todo `tareas/`.
