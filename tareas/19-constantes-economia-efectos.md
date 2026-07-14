# T19 — Constantes de economía nombradas + efectos de ítems declarativos

**Estado:** ✅ hecho (2026-07-02, A+C junto a T15) · **Esfuerzo global:** S-M · **Depende de:** T9 · **Se ejecuta junto a:** T15-A (RewardMath)

## El quiebre (por qué)

Los números que definen cuánto vale el esfuerzo están regados por el código sin nombre: `difficulty * 10`, `* 5`, `0.25`, `0.1`, los rendimientos `0.5/0.25`, el `×2` de frenesí, el `1.2` del ritual, `-5` de hambre cada 2 h, los umbrales de evolución. Cambiar el balance exige buscar literales por el repo y rezar por no confundir el 0.25 de "fuera de contexto" con el 0.25 de "más de 10 tareas al día". Y el caso más peligroso no es un número sino un string: el efecto curativo de la Poción se despacha por **substring del nombre del ítem** — cualquier ítem futuro cuyo nombre contenga "Poción" curará 50 HP sin que nadie lo haya decidido.

## Evidencia en el código

- `src/PetProductivity.Server/Services/PetService.cs:114-160` — la cadena completa: `difficulty * 10` (:114), `difficulty * 5` (:116), `0.25` fuera-de-contexto (:121-122), plausibilidad `/10` (:127-128), dedupe `0.1` (:141-142), dims `0.5/0.25` con cortes en 5/10 tareas (:149), frenesí `*= 2` (:156).
- `PetService.cs:162,361` — el `1.2` del ritual escrito dos veces (comparación implícita `> 1.0` y asignación).
- `PetService.cs:224-225` — hambre `+ difficulty * 5`, heal `difficulty * 2`.
- `HealthDecayHostedService.cs:11,58,64` — tick 2 h, −5 hambre, −5 HP.
- `Pet.cs:76-82` — umbral de revivir `>= 9`, HP al revivir `* 0.2`, gracia 24 h.
- `PetEvolution.cs:20-27` — los umbrales SÍ están nombrados (el patrón correcto ya existe en el repo).
- `ShopController.cs:84-85` — `if (request.ItemName.Contains("Poción")) pet.Heal(50);` — despacho de efectos por substring del nombre.
- `Shared/Constants` — existe (`PhotoBonusMultiplier`, `BaseUrl`)… con 2 usos en todo el repo: la casa está construida y vacía.

## Opciones

### A. Constantes nombradas dentro de `RewardMath` (junto a T15-A, recomendada)
T15-A ya planea extraer el cálculo puro de recompensas a `RewardMath` (patrón `FocusMath` existente). Esta tarea es su mitad gemela: al mover el cálculo, cada literal se convierte en `const` con nombre (`XpPerDifficulty = 10`, `OutOfContextFactor = 0.25`, `DuplicateFactor = 0.1`, `DailyDimAfter = 5`, `FrenzyMultiplier = 2`, `RitualMultiplier = 1.2`…). Los números de decadencia/Fénix van como consts en sus clases (`HealthDecay`, `Pet`), no todos a un cajón global.
- **Pros:** un solo lugar para leer/ajustar el balance; los tests de tabla de T15 referencian las consts (cambiar balance = cambiar const + casos, el compilador guía); cero cambio de comportamiento.
- **Contras:** ninguno real si se hace CON T15-A; hacerlo como pasada aparte duplicaría el toque al método más delicado del server.
- **Esfuerzo:** S (incremental sobre T15-A) · **Toca:** server + shared.

### B. Cajón global `GameBalance` en Shared con TODO
Una clase única con todas las constantes del juego (economía, decadencia, evolución, Fénix, tienda).
- **Pros:** un archivo que es "el manual de balance".
- **Contras:** acopla Shared a detalles del server que el cliente no necesita; los números lejos de su lógica pierden contexto (¿`Five = 5` de qué?). El punto medio de A (consts junto a su dominio, `RewardMath` para la economía) da el 90% con mejor cohesión.
- **Esfuerzo:** S-M · Descartable frente a A.

### C. Efectos de ítems declarativos en el catálogo (mata el `Contains("Poción")`)
Campo `Effect` en `ShopItem`/`info.json` (ej. `"effect": "heal", "effectValue": 50`); `BuyItem` despacha por un `switch` sobre el efecto, no sobre el nombre. Los ítems sin efecto (cosméticos, la mayoría) no declaran nada.
- **Pros:** el efecto es una decisión explícita del catálogo, no una coincidencia de naming; añadir consumibles futuros (el congelador de racha de T1-D, más pociones) es editar JSON, no código; el catálogo ya es la única fuente de verdad de precios/disponibilidad — esto lo completa.
- **Contras:** toca `ShopItem` (shared), el loader y los `info.json` existentes (la Poción actual gana su campo); un `switch` de efectos es un mini-sistema — mantenerlo en 2-3 efectos, no inventar un motor de scripting.
- **Esfuerzo:** S-M · **Toca:** shared + server + catálogo.

### D. Statu quo con comentarios
Nombrar cada literal con un comentario al lado.
- **Pros:** nada que mover.
- **Contras:** los comentarios no participan en tests ni refactors; el `Contains("Poción")` seguiría armado. Descartable.

## Recomendación

**A + C.** A se agenda literalmente como parte de T15-A (mismo PR: extraer + nombrar); C es independiente y pequeña — puede ir antes o después, pero sí antes de que T1-D (congelador de racha) añada el segundo consumible, porque ese ítem necesitará el despacho declarativo.

## Criterios de éxito / verificación

1. `grep -n "0\.25\|0\.1\|\* 10\|\* 5" src/PetProductivity.Server/Services/PetService.cs` → solo referencias a constantes nombradas (o nada, si vive en RewardMath).
2. La tabla de tests de T15 pasa sin cambiar valores esperados (la extracción no cambió el balance).
3. `grep -rn "Contains(\"Poción" src/` → vacío; comprar la Poción sigue curando 50 (verificado en emulador).
4. Un ítem nuevo de prueba con `"effect": "heal"` en su `info.json` cura sin tocar C#.
5. Crear un ítem llamado "Poción decorativa" SIN campo effect → no cura (el bug fantasma, cerrado).

## Dependencias

- **T15-A** es el vehículo de A (misma pasada).
- **T1-D** (congelador de racha) debe llegar después de C para nacer declarativo.
- **T9** primero, como todo.

## Resultado (2026-07-02) — A+C (commit cbe4c6f)

- **A:** todos los números de la economía como `const` nombradas en `RewardMath` (XpPerDifficulty, OutOfContextFactor, DuplicateFactor, DailyDim*, FrenzyXpMultiplier, RitualMultiplier+RitualResetDifficulty, Hunger/HealPerDifficulty); decaimiento en `HealthDecayHostedService` (HungerPerTick, StarvingDamagePerTick); Fénix en `Pet` (ReviveDifficulty, ReviveHealthFraction, GraceHours); margen de evolución en `PetService` (DominantStatMargin). Criterio 1 ✓ (grep limpio en PetService) y 2 ✓ (la tabla pasó sin cambiar valores).
- **C:** `ShopItem.Effect`/`EffectValue` leídos del `info.json` (CatalogLoader); `BuyItem` despacha por `switch` sobre `Effect` — `Contains("Poción")` eliminado (criterio 3 grep ✓). La Poción declara `"effect":"heal","effectValue":50`. **Verificado en vivo:** daño dev a 20 HP → compra → **70 HP (+50 exactos)**. Criterio 5 por construcción (ítem sin `effect` = switch no-op). Ítems nuevos con efecto = editar JSON, sin C# (criterio 4).
