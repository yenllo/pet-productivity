# T26 — Balance numérico de la economía

**Estado:** ✅ hecho (2026-07-02; análisis — los cambios se aplican aparte vía T19-A) · **Tipo:** revisión (hoja de cálculo, no código) · **Esfuerzo:** S-M · **Depende de:** —

## Por qué esta revisión

Los análisis previos miraron la *estructura* de las recompensas (qué multiplicadores existen, en qué orden), nunca los *números* como sistema económico. No sabemos si el ritmo y la economía están donde se quiere: se revisa con una hoja de cálculo y el catálogo abierto, no con grep. Alimenta directamente decisiones de otros planes (precio del congelador de T1-D, ritmo de T4).

## Qué revisar (checklist)

1. **Oro por día de un usuario típico vs. precios del catálogo:** abrir `Catalog/` (nunca se leyó) y listar precios. Con `gold = difficulty * 5` y ~3-5 tareas/día, ¿cuánto oro/día entra? ¿Cuántos días para el ítem más caro? ¿Se siente alcanzable o inflado/trivial?
2. **Ritmo de evolución:** Master a 501 XP, `xp = difficulty * 10`. ¿Los 10-15 días estimados son deseados o accidentales? (decide la urgencia de T4).
3. **Valor de los multiplicadores:** ¿el ×1.2 del ritual, el ×2 de foto y el ×2 de frenesí se combinan de forma que rompe la curva (una tarea perfecta vale demasiado)? ¿O son irrelevantes en la práctica?
4. **Castigos:** −5 hambre/2 h → ~7 días de abandono total hasta cristal. ¿Es la ventana correcta? (cruza con T3-escudo de ausencia).
5. **La Poción:** precio vs. 50 HP curados vs. cuánto cuesta recuperar HP con tareas. ¿Vale la pena comprarla o es un ítem muerto?
6. **Sinks de oro:** hoy el oro solo compra cosmético + Poción. ¿Hay suficiente en qué gastarlo, o se acumula sin destino? (justifica T1-D congelador).
7. **Rendimientos decrecientes:** el corte en 5/10 tareas con ×0.5/×0.25 — ¿desincentiva el uso intenso legítimo, o está bien calibrado contra el spam?

## Cómo

- Hoja de cálculo: modelar un usuario "constante" (X tareas/día de dificultad media) y uno "intenso"; graficar oro acumulado, XP acumulado y HP a lo largo de 30 días.
- Contrastar con los precios reales de `Catalog/`.
- No cambiar números aquí: la SALIDA es una recomendación de balance; los cambios se aplican vía T19-A (constantes) con la red de tests de T15-A puesta.

## Salida esperada

Un veredicto por métrica (bien / subir / bajar) con el número propuesto, listo para que T19-A lo aplique. Y respuestas concretas a: ¿precio del congelador de racha? ¿es urgente T4?

## Dependencias

- Independiente para *analizar*; para *aplicar* cambios necesita **T15-A** (tests) + **T19-A** (constantes). Alimenta **T1-D** (precio congelador), **T3** (ventana de abandono) y **T4** (ritmo).

## Resultado (2026-07-02) — metas del dueño: ítem top y Egg→Master ambos en **2-4 semanas**

**Modelo:** usuario constante = 4 tareas/día de dificultad ~4, plausibilidad ~9 (medida en vivo) → **~144 XP y ~72 oro/día** (con ritual: ~172 XP). Catálogo real (186 ítems en oro): min 50 · mediana 200 · max 500; por categoría: Decoración med.110, Estructural 140, Muebles 240, Vida 290-330, Estilos 250-300.

| # | Métrica | Hoy | Veredicto | Propuesta |
|---|---------|-----|-----------|-----------|
| 1 | Oro/día vs. precios | 72/día; mediana=3 días ✓; **top (500)=7 días** | mediana bien; top corto para la meta | subir el techo: Cristal Evolutivo 500→**1500** y 2-3 ítems aspiracionales 1000-2000 (14-28 días) |
| 2 | Ritmo evolución | Master 501 XP = **~3 días** (no 10-15) | MUY rápido vs. meta 2-4 sem | umbrales **50 / 600 / 2500** (Egg→Baby igual: dopamina día 1; Adult ~día 4; Master ~día 15-17). Aplicar en `PetEvolution` + actualizar tabla T15 |
| 3 | Multiplicadores | día perfecto (ritual+frenesí+foto, foco d8) = 384 XP ≈ 2.5 días típicos en 1 tarea | aceptable CON umbrales nuevos (15% del camino a Master); hoy rompe la curva (75%) | mantener; si duele tras medir, frenesí ×2→×1.5. No apilar nuevos ×2 |
| 4 | Castigos | hambre 40 h + salud 40 h = **cristal a ~3.3 días** (no ~7) — pero HOY el decay casi no corre (Render duerme; ver T10) | ventana corta para humanos (finde malo = cristal) y se volverá REAL al activar UptimeRobot | `StarvingDamagePerTick` 5→**3** (~5.5 días) + escudo de ausencia (T3). ⚠️ activar el monitor = activar la muerte real: hacerlo consciente |
| 5 | Poción (50 oro / 50 HP) | <1 día de ingreso; tareas curan gratis d×2 | precio OK como parche de emergencia; hoy casi sin uso porque el decay no corre | mantener 50; UX: deshabilitar compra con HP lleno (T27) |
| 6 | Sinks de oro | catálogo total ≈ 37k oro (~500 días, colección) pero **cero sink repetible** | falta destino recurrente | congelador de racha (T1-D) a **200 oro** (~3 días); considerar consumibles menores repetibles |
| 7 | Rendimientos decrecientes | 6ª tarea ×0.5, 11ª ×0.25 — **el foco también los sufre** | bien contra spam de texto; injusto con el grind legítimo verificado por tiempo | **exentar al foco** (contar solo tareas de texto en `todayCount`, o saltar dims si `timedDifficulty`) |

**Respuestas pedidas:** precio congelador T1-D = **200 oro**. ¿T4 urgente? — **No** con los umbrales nuevos (Master pasa de 3 a ~16 días); T4 sigue siendo la última de la cola.

**APLICADO con OK del dueño (2026-07-02, commit `1d02c5c`):** umbrales 50/600/2500, `StarvingDamagePerTick` 5→3, foco exento de rendimientos decrecientes, Cristal Evolutivo 500→1500. Tests actualizados (52 verdes). Nota: mascotas existentes con 601-2500 XP volvieron a Adult (decisión consciente pre-lanzamiento).

**APLICADO (2026-07-03):** Frenesí bajado de **×2 a ×1.5** (`RewardMath.FrenzyXpMultiplier`; tabla de tests actualizada). **Pendiente = TAREA A FUTURO** (decisión del dueño): los 2-3 ítems aspiracionales de 1000-2000 oro (como meta de largo plazo; van bien con el arte de T5/F4). No se implementan aún.
