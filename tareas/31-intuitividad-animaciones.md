# T31 — Análisis: intuitividad + animaciones (2026-07-18)

**Estado:** ✅ IMPLEMENTADO (2026-07-18, los 9 ítems; ver "Implementación" al final) · **Método:** lectura completa de las 14 vistas del cliente +
grafo graphify (mapa de comunidades: confirma que NO existe ningún código de onboarding/tutorial en
todo el corpus) · **Base:** continúa T27 (cuyo criterio 1 — "¿se entiende sin explicación?" — quedó
explícitamente sin cerrar).

## Diagnóstico en una línea

La app ya tiene buen pulido *reactivo* (T27 arregló 29 fricciones), pero sigue siendo un juego que
**nunca enseña sus reglas**: cero onboarding conceptual, ~10 conceptos de juego sin explicación en
pantalla, y el feedback dominante es texto (51 `DisplayAlert`/`Toast` vs 20 llamadas de animación en
todo el cliente).

---

## A. Intuitividad — lo que la app nunca explica

El único mecanismo de explicación de TODA la app es el tap del chip de racha
(`ExplainStreakCommand` → toast). Todo lo demás se aprende por prueba y error:

1. **El bucle central no se enseña.** Tras la ceremonia aterrizas en un Dashboard con Hambre,
   Salud, Evolución, 4 círculos de Crecimiento y un Ritual 3×3 — sin una palabra de qué es nada.
   Nadie dice "describe lo que hiciste → una IA lo juzga → tu mascota crece". El placeholder del
   Editor ayuda, pero no explica el juicio de dificultad ni la recompensa dual XP/Oro.
2. **"Reclamar recompensa" vs "⏱ Modo foco":** dos caminos lado a lado sin pista de cuándo usar
   cuál (texto libre juzgado vs sesión cronometrada con bloqueo).
3. **Cuerpo/Mente/Hogar/Bienestar** aparecen en 3 pantallas (Dashboard, Perfil, Stats) y en
   ninguna se dice qué son ni que la IA categoriza tu tarea hacia una de las 4 dimensiones. Sin
   eso, los círculos son decoración.
4. **Hambre/Salud:** bajan solas (`HealthDecayHostedService`) y el usuario no sabe por qué ni cómo
   subirlas. Barras que decaen sin explicación = ansiedad o indiferencia.
5. **Fénix/cristalización:** el overlay del cristal explica BIEN las dos puertas de regreso — pero
   solo cuando ya te pasó. No hay aviso previo ("si la abandonas se cristaliza") ni mención del
   escudo de gracia de 24 h. La primera cristalización se siente castigo arbitrario.
6. **Ritual diario:** la grilla 3×3 no dice que es tres-en-raya ni que completar línea da ×1.2. El
   chip "XP x1.2" aparece sin contexto. El único hint existente es del modo renombrar.
7. **Social (PetDetailPage):** "Afecto %" por miembro, emoji de humor, semáforo de colores,
   countdown de auto-aprobación — ninguno explicado. El anti-polizón (×0.25 fuera de contexto,
   afecto que decae) es invisible hasta que golpea. Frenesí solo se explica cuando ya está activo.
8. **Tienda:** amarillo = "te alcanza" es convención propia sin leyenda; el concepto
   Guardados/lápiz ✏️ solo se descubre leyendo un toast largo de compra.
9. **Ya anotado en T29:** quien se registra directo nunca nombra a su mascota ni ve la ceremonia.

## B. Animaciones y feedback — lo que existe y lo que falta

**Ya existe (y está bien):** celebración de recompensa con pop + háptica (T27 #20), respiración y
salto de la mascota dentro del diorama (un solo timer), entrada escalonada del Dashboard (1ª vez),
ceremonia de nacimiento (GIF + flash), zoom de inmersión del sandbox, rombo rojo de movimiento
inválido, egg crack sincronizado en grupo, anillo SkiaSharp del foco.

**Lo que falta, por impacto:**

1. **Barras sin tween.** `RoundedBar` (Hambre, Salud, Evolución, meta de foco) salta al valor nuevo
   sin transición. Ver llenarse la barra ES la recompensa; hoy es teleport. Es UN control → animar
   ahí mejora toda la app de golpe. *(Mayor palanca del análisis.)*
2. **Números flotantes.** +XP/+Oro no flotan hacia sus contadores; el chip de oro cambia de valor
   en seco. El "+20 🪙 que vuela al chip" es el patrón estándar de juegos y no existe.
3. **Ritual sin celebración.** Marcar celda = cambio de color por DataTrigger (sin pop); completar
   la línea del tres-en-raya (¡el momento ×1.2!) no celebra nada.
4. **Evolución silenciosa.** El overlay de evolución es estático (emoji ✨ + texto). El momento más
   importante del juego tiene menos animación que el nacimiento. (El propio XAML lo admite: "hito
   hoy silencioso".)
5. **Compra = toast de texto.** Ni el ítem vuela al cuarto ni el oro cuenta hacia abajo.
6. **Overlays aparecen en seco.** Estado/ritual/código de grupo togglean `IsVisible` sin fade (la
   celebración y el sandbox sí animan — el patrón existe, falta aplicarlo parejo).
7. **Frenesí estático.** Un banner llamado "🔥 ¡FRENESÍ! ×2" que no pulsa ni arde.
8. **Lenguaje de feedback desbalanceado:** 51 alerts/toasts vs 20 animaciones. T29 ya anota que los
   `DisplayAlert` nativos desentonan con el look Neo-Retro (flujo de foto).

## C. Recomendaciones priorizadas (frecuencia × dolor)

| # | Qué | Costo | Por qué primero |
|---|-----|-------|-----------------|
| 1 | **Tween en `RoundedBar`** (animar `Progress` en el propio control) | XS | Un archivo, toda la app gana |
| 2 | **Onboarding mínimo**: 3 tarjetas overlay en la primera visita al Dashboard (bucle central → vitales/decadencia → ritual ×1.2), con pref de primera vez (patrón ya existe) y reusando el patrón de overlays | S | Cierra el criterio 1 de T27; se ve 1 vez pero define si el usuario entiende el juego |
| 3 | **Patrón "tap = explicación" extendido**: replicar `ExplainStreak` en Crecimiento, Hambre/Salud, Afecto, humor, chip ×1.2, amarillo de tienda | S | Barato por unidad; convierte cada concepto opaco en autoexplicable |
| 4 | **Números flotantes +XP/+Oro** hacia sus contadores | S | El feedback de mayor frecuencia (cada tarea) |
| 5 | **Celebración de línea del ritual** (pop de celda + destello de línea) | S | Momento ×1.2 hoy mudo |
| 6 | **Evolución con animación real** (flash + scale, reusar assets de la ceremonia) | S | Momento cumbre, hoy estático |
| 7 | **Fade helper único para overlays** + pulso del banner Frenesí | XS-S | Coherencia; quita el "aparece en seco" |
| 8 | Compra animada (oro cuenta hacia abajo) | M | Menor frecuencia que 4-6 |
| 9 | Aviso previo de cristalización (banner cuando Salud < umbral: "tu mascota está en peligro") | S | Convierte el castigo arbitrario en tensión anunciada |

Notas de ejecución: 1-7 son solo cliente (sin server, sin migraciones). El patrón de overlay + pref
de primera vez ya existe en el código (celebración, sandbox, `_entered`, pref de etapa celebrada) —
nada de esto necesita infraestructura nueva. Respeta la política de T29: nada se implementa sin
decisión del dueño.

---

## Implementación (2026-07-18) — los 9 ítems, decididos por el dueño

Decisiones: alcance completo (1-9), explicaciones como **mini-overlay estilizado** (no toasts),
onboarding **Dashboard (3 tarjetas) + 1 tarjeta la 1ª vez en Familias/Tienda/Foco**, y el
onboarding se muestra a TODOS una vez (detectar "usuario nuevo" es frágil; nadie vio esto explicado).

**Archivos nuevos:** `Services/Anim.cs` (Pop/FadeIn/FloatUp/Count — factoriza el pop de TaskPage),
`Controls/InfoOverlay.xaml(.cs)` (overlay explicativo reutilizable, `IsOpen` TwoWay + pop),
`Services/Onboarding.cs` (flags `Onboard_*` en Preferences; logout NO las limpia a propósito).

**Qué quedó dónde:**
- Tween de barras: `RoundedBar` anima `Progress` (450 ms CubicOut; layout inicial en seco).
- Tap = explicación (`ExplainCommand` + `InfoOverlay`): Crecimiento, Hambre/Salud y chip ×1.2
  (Dashboard), fila de miembro (PetDetail), chip de oro (Tienda). `ExplainStreak` migrado del toast.
- Aviso de cristalización: banner rojo con `ShowDangerBanner` (Salud<30) → overlay "El cristal Fénix".
- Onboarding: 3 tarjetas con dots en Dashboard (`ShowOnboarding`/`OnboardingStep`), 1 tarjeta en
  Hub/Shop/Focus vía `ShowOnboardCard()`; fila "¿Cómo se juega?" en Ajustes (resetea flags y navega).
- Celebraciones: +XP/+Oro flotan en TaskPage; chip de oro del Dashboard cuenta (`GoldDisplay` +
  `_lastSeenGold`); ritual con pop por celda y destello de línea (`RitualCellPopped`/
  `RitualLineCompleted`, solo transición y solo por acción del usuario — `celebrate:` en
  `UpdateRitualGrid`); evolución con flash+spring; oro de la tienda cuenta en `RefreshAll`.
- Pulso del banner Frenesí colgado del `FrameTick` existente de `RoomDiorama` (sin timers nuevos).
- `L.cs`: sección `// ---- T31 ----` con todas las claves EN nuevas ("Modo foco" colisionaba →
  título "Tu primer foco").

**Verificado:** compila por lote y al final (0 errores; las advertencias nuevas no son de archivos
T31). **Pendiente del dueño:** pasada visual en emulador/teléfono (recorrido del plan: instalación
limpia → onboarding → tarea → ritual → taps → compra → "¿Cómo se juega?", y repetir 2-3 overlays
en English).
