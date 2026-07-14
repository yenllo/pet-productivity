# T1 — Racha diaria real

**Estado:** ✅ hecho (2026-07-03: B+F+D; E diferida a cuando haya arte) · **Esfuerzo global:** M · **Depende de:** T8 (corte de día local)

## El quiebre (por qué)

No existe ninguna razón para abrir la app *hoy* específicamente. La "racha" que ve el usuario (🔥 en Perfil, "días" en Stats) es en realidad un contador de tareas totales: sube 3 si haces 3 tareas en un día y no baja nunca. Una racha diaria real —que se pierde si no haces nada hoy— es el ancla de hábito más probada que existe (Duolingo la considera su mecánica de retención #1). La app ya paga el costo de tener el concepto (lo muestra en dos pantallas) sin cobrar ninguno de sus beneficios.

## Evidencia en el código

- `src/PetProductivity.Server/Services/PetService.cs:232` — `user.CurrentStreak++;` dentro de `ApplyRewardAsync`: incrementa **por tarea**, nunca se resetea.
- `src/PetProductivity.Client/Views/StatsPage.xaml:31` — se muestra como `'{0} días'`.
- `src/PetProductivity.Client/Views/ProfilePage.xaml:49` — `'🔥 {0} racha'`.
- `src/PetProductivity.Server/Controllers/FocusController.cs:89-94` — **el patrón correcto ya existe**: `FocusStreak` compara `LastFocusDate` con hoy/ayer y resetea a 1 si hubo hueco. Es copy-paste conceptual.
- `User` ya tiene `MaxFocusStreak` como precedente de "racha máxima histórica".

## Opciones

### A. Racha diaria server-side con `LastActivityDate` (patrón `FocusStreak`)
Añadir `LastActivityDate` (y opcionalmente `MaxStreak`) a `User`; en `ApplyRewardAsync` reemplazar el `++` por la lógica de FocusController.cs:89-94. Migración EF aditiva.
- **Pros:** mínimo, reutiliza patrón probado del mismo repo, arregla el dato en el origen (las 2 pantallas quedan correctas sin tocarlas).
- **Contras:** la racha "rota" solo se detecta al completar la siguiente tarea (lazy), no a medianoche — suficiente para mostrar, pero el aviso "tu racha muere hoy" necesita cómputo aparte (ver T2).
- **Esfuerzo:** S · **Toca:** server + migración BD.

### B. Racha unificada: "hice algo hoy" (tarea O foco O ritual)
Igual que A, pero `LastActivityDate` se actualiza desde los tres puntos de entrada: `ApplyRewardAsync`, `FocusController.Complete` y `ToggleRitualCell`.
- **Pros:** más justa (un foco de 60 min sin describir tarea también cuenta); evita rachas paralelas confusas (hoy ya existe `FocusStreak` aparte).
- **Contras:** el ritual como fuente es trampeable (togglear una celda = mantener racha); decidir si el ritual cuenta o no.
- **Esfuerzo:** S-M · **Toca:** server + migración BD.

### C. Solo renombrar la UI ("tareas totales")
Cambiar los `StringFormat` para que digan lo que el número es.
- **Pros:** 2 líneas, elimina la mentira.
- **Contras:** no retiene a nadie; renuncia a la mecánica. Solo válida como parche si T1 se pospone.
- **Esfuerzo:** S · **Toca:** cliente (XAML).

### D. Congelador de racha (ítem de oro)
Ítem del catálogo (`Catalog/` + `info.json`, mismo flujo que ShopController `buy`) que se **auto-consume** cuando la racha se rompería: si `LastActivityDate` fue anteayer y hay congelador en `user.Inventory`, se descuenta 1 y la racha sobrevive.
- **Pros:** primer sink de oro **no cosmético** (hoy el oro solo compra estética y pociones); reduce la frustración del día malo, que es cuando la gente abandona; Duolingo demostró que la gente paga por esto.
- **Contras:** requiere que A o B exista primero; decidir precio (caro: ~oro de 2-3 días de uso normal); tensiona levemente la regla "oro = cosmético" (documentado: no compra XP ni evolución, solo protege una métrica social).
- **Esfuerzo:** M · **Toca:** server + catálogo + UI de tienda (categoría "consumibles").

### E. Hitos de racha (7 / 30 / 100 días)
Al cruzar un hito: oro bonus y/o cosmético exclusivo (mueble del diorama "trofeo racha 30").
- **Pros:** convierte la racha en metas escalonadas (la meta próxima siempre visible); los cosméticos exclusivos alimentan el diorama.
- **Contras:** necesita arte para los cosméticos; sin A/B no tiene base.
- **Esfuerzo:** M · **Toca:** server + arte + diorama.

### F. Racha prominente en el Dashboard + estado "en riesgo"
Mostrar en la pantalla principal (hoy solo está en Perfil/Stats): 🔥 N + indicador "aún no hiciste nada hoy". El cliente ya re-hidrata `/api/users/{id}` tras cada acción, así que el dato llega gratis; solo falta exponer `LastActivityDate` para computar "en riesgo".
- **Pros:** la racha solo funciona si se ve cada vez que abres la app; el estado "en riesgo" es el gatillo emocional.
- **Contras:** espacio en un Dashboard ya cargado (diorama + barras + ritual).
- **Esfuerzo:** S · **Toca:** cliente.

## Recomendación

**B + F primero** (racha real unificada + visible en Dashboard), con el ritual **excluido** como fuente (demasiado trampeable). Después **D** (congelador) como primer consumible de la tienda, y **E** como capa final cuando haya arte. C se descarta salvo que T1 entero se postergue.

Implementar el corte de día con el helper de **T8** desde el día uno — si la racha nace en UTC habrá que migrar datos después.

## Criterios de éxito / verificación

1. Completar una tarea hoy → racha 1; otra tarea el mismo día → sigue 1; tarea mañana → 2; saltarse un día → vuelve a 1.
2. Un foco completado (sin tarea de texto) también mantiene la racha.
3. Con congelador en inventario, saltarse 1 día no rompe la racha y el inventario baja en 1.
4. El Dashboard muestra la racha y el estado "en riesgo hoy" cambia al completar la primera actividad del día.
5. Test xUnit del cálculo (mismo día / día siguiente / hueco / congelador).

## Dependencias

- **T8** define qué es "hoy" (sin ella, la racha se rompe a las 8-9 pm hora de Chile).
- **T2** usará `LastActivityDate` para el push nocturno "tu racha muere en 3 h".

## Resultado (2026-07-03) — B + F + D

- **B (racha real unificada):** `User.LastActivityDate` + `MaxStreak` (migración `AddDailyStreak`); helper `DailyStreak.Advance` llamado desde `ApplyRewardAsync` — cubre tareas de texto Y foco (ambos pasan por ahí); ritual excluido (trampeable), como recomendaba el plan. Corte de día = `LocalDay` (T8) desde el día uno. Las rachas infladas viejas se resetean a 1 con la primera actividad (el contador anterior no significaba nada).
- **D (congelador):** `Catalog/Consumibles/Congelador_de_Racha` (200 oro — precio de T26, ~3 días de ingreso; `Rare`, 🧊, sin `effect` inmediato: vive en el inventario). Auto-consumo en `DailyStreak`: cubre exactamente 1 día de hueco; huecos mayores no lo desperdician. Primer sink de oro no cosmético (documentado: no compra XP ni evolución).
- **F (visible):** chip `🔥 N` en la tira de estado del Dashboard con punto naranja = "aún no hiciste nada hoy" (`LastActivityDate` vs. fecha local del dispositivo). Stats/Perfil quedaron correctos gratis (mismo `CurrentStreak`).
- **Verificación:** criterio 1 en vivo (2 tareas mismo día → racha 1; `lastActivityDate` = token local); criterios 1/3/5 con 7 tests de `DailyStreak` (mismo día/siguiente/hueco/congelador/consumo/no-desperdicio/max). Suite: **70 verdes**. Congelador visible en catálogo ✓. Criterio 4 (visual) pendiente de la revisión del dueño.
- **E (hitos 7/30/100):** diferida — necesita arte (trofeos del diorama); va con T4/T5.
