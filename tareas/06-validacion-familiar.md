# T6 — Validación familiar: esfuerzo que expira en silencio

**Estado:** ✅ hecho (2026-07-03: B+D+E) · **Esfuerzo global:** S-M · **Depende de:** T2 (push) para la mitad del valor

## El quiebre (por qué)

Toda tarea de mascota de grupo espera la aprobación de la mayoría de los **demás** miembros. Si la familia es pasiva, la aprobación caduca a los 7 días y **se borra sin premiar ni avisar**: hiciste el esfuerzo, lo reportaste, y desapareció. Es el anti-patrón exacto de la motivación (esfuerzo → nada) aplicado dentro de la capa social, que debería ser el multiplicador de engagement, no su fuga. Y del otro lado, el aprobador ni se entera de que alguien lo espera.

## Evidencia en el código

- `src/PetProductivity.Server/Services/PetService.cs:167-192` — toda tarea de grupo (salvo foco) va a `TaskApprovals`; el requester recibe "Tarea enviada a validación…" y queda esperando.
- `src/PetProductivity.Server/Services/PetService.cs:271-274` — se necesita mayoría estricta de los otros miembros (`others/2 + 1`).
- `src/PetProductivity.Server/Services/FocusCleanupHostedService.cs:43,54` — `approvalCutoff = -7 días` → `RemoveRange`: la aprobación vencida se **elimina**, la recompensa ya calculada (`XpEarned`, `GoldEarned` guardados en la fila) se pierde.
- Evento SignalR `TaskPending` existe (`PetService.cs:181`) pero solo llega si la app está abierta y conectada; no hay push FCM para esto (`PushService` hoy solo lo usa el Frenesí).
- CLAUDE.md ya registra como pendiente 🟡 que el ítem de solicitudes no muestra ni el nombre del solicitante — la UI de pendientes está desnutrida en general.

## Opciones

### A. Auto-aprobar al expirar (en vez de borrar)
En `FocusCleanupHostedService`, las aprobaciones que cruzan el cutoff se **aplican** (llamar `ApplyRewardAsync` con los valores ya guardados en la fila) en lugar de `RemoveRange`. Semántica: "el silencio de la familia otorga".
- **Pros:** cambio mínimo (el servicio, la fila y `ApplyRewardAsync` ya existen); elimina el caso "esfuerzo → nada" por completo; no cambia el flujo feliz.
- **Contras:** un tramposo con familia pasiva cobra todo con solo esperar — mitigado porque el anti-cheat real ya actuó antes (plausibilidad de la IA, dedupe, rendimientos decrecientes se aplican al calcular `XpEarned`); 7 días es demasiado para "auto-aprobado" (combinar con B).
- **Esfuerzo:** S · **Toca:** server.

### B. Timeout corto (48 h) + auto-aprobación
Igual que A pero con cutoff de 48 h: la familia tiene 2 días para vetar; si no, se premia.
- **Pros:** la recompensa llega con el evento aún fresco (7 días después ya no refuerza nada); mantiene el veto con sentido.
- **Contras:** familias que se conectan poco casi nunca vetarán a tiempo → en la práctica el voto se vuelve simbólico (aceptable: el voto es ritual social, el anti-cheat es la IA).
- **Esfuerzo:** S (mismo cambio que A + constante) · **Toca:** server.

### C. Auto-aprobar dificultad baja (≤3) al instante
Las tareas triviales de grupo premian directo (como las personales); el voto queda solo para claims medianos/grandes.
- **Pros:** quita fricción del caso más frecuente (lavar platos no necesita jurado); menos pendientes acumulados → los que llegan sí se atienden.
- **Contras:** umbral arbitrario; un spam de tareas ≤3 esquiva el voto (pero los rendimientos decrecientes de `PetService.cs:146-151` ya capan eso); reduce las ocasiones de interacción social (el voto también es contacto).
- **Esfuerzo:** S · **Toca:** server (una condición en `ProcessTaskCompletion`).

### D. Push "tu familia espera tu aprobación"
Al crear la `TaskApproval`, además del evento SignalR, `PushService.SendToUsersAsync` a los demás miembros: "Renzo hizo «terminar informe» — ¿lo apruebas?". Opcional: recordatorio único a las 24 h si sigue pendiente.
- **Pros:** ataca la causa raíz (nadie vota porque nadie se entera); es además un gancho de re-engagement para el aprobador — cada tarea de tu pareja es una razón para abrir la app tú; infraestructura lista y verificada en producción.
- **Contras:** volumen: en grupos activos puede ser ruidoso → agrupar ("3 tareas esperan tu voto") y respetar el rate-limit por tipo de T2.
- **Esfuerzo:** S-M · **Toca:** server (+ política anti-spam de T2).

### E. UI de pendientes con countdown
En el detalle del grupo: lista de aprobaciones con solicitante, descripción, recompensa en juego y "se auto-aprueba en 36 h". De paso arregla el pendiente 🟡 del nombre del solicitante.
- **Pros:** hace visible la mecánica (hoy el requester no sabe ni cuánto esperará); el countdown presiona suavemente al votante.
- **Contras:** solo tiene sentido tras A/B (countdown hacia el borrado sería mostrar la fuga en vivo).
- **Esfuerzo:** M · **Toca:** cliente + endpoint que exponga `CreatedAt`/deadline.

## Recomendación

**B + D + E** (en ese orden): auto-aprobación a las 48 h convierte el peor caso en "recompensa con retraso", el push hace que el voto real ocurra antes del timeout, y la UI con countdown hace legible todo el sistema. **C** opcional después, si en uso real las familias reportan fatiga de votar trivialidades. **A** queda subsumida en B (mismo cambio, mejor constante).

Principio rector: el voto familiar es un ritual de atención mutua, no el anti-cheat — el anti-cheat ya lo hacen la IA, el dedupe y los rendimientos decrecientes. Diseñarlo como veto opcional, no como compuerta obligatoria.

## Criterios de éxito / verificación

1. Tarea de grupo sin ningún voto: a las 48 h la mascota recibe el XP/oro exactos guardados en la fila, y el requester ve la recompensa (evento `TaskApproved` o equivalente).
2. Un veto/aprobación dentro de la ventana sigue funcionando igual que hoy (mayoría de los otros).
3. Al crear la aprobación, los demás miembros reciben push (1 por tarea, agrupado si hay varias).
4. La UI del grupo muestra solicitante + countdown de cada pendiente.
5. Test xUnit: aprobación vencida se aplica (no se borra) y no se aplica dos veces si el tick corre dos veces (idempotencia).

## Dependencias

- **T2** para el push y su política anti-spam.
- Sin dependencia de datos nuevos: `TaskApproval` ya guarda todo lo necesario (`CreatedAt`, `XpEarned`, `GoldEarned`).

## Resultado (2026-07-03) — B + D + E

- **B (48 h y el silencio otorga):** `FocusCleanupHostedService` ya no borra — llama `PetService.AutoApproveExpiredAsync` por cada vencida (const `ApprovalAutoApproveHours = 48`). Idempotente por diseño: lock por approvalId (el de T11-D2, que ya contemplaba a este "aprobador" extra) + re-fetch. La aplicación se extrajo a `ApplyApprovalAsync`, compartida con el voto por mayoría (criterio 2 intacto).
- **D (push "tu familia espera"):** al crear la `TaskApproval`, push a los demás miembros ("🗳️ {nombre} hizo «…» — tu aprobación premia a {mascota}"), tipo `approval` bajo `NotificationPolicy` (máx. 1/día por usuario = el "agrupado" anti-ruido; quiet hours incluidas).
- **E (countdown):** `PendingTaskDto.HoursLeft` (server calcula contra el cutoff real) + "⏳ se aprueba sola en N h" en la tarjeta de pendientes del detalle del grupo (que ya mostraba solicitante y votos).
- **Verificación:** test `AutoApprove_AplicaUnaVez_YEsIdempotente` (criterios 1 y 5: XP exacto de la fila, segundo tick no-op). Suite: **83 verdes**; cliente compila. Push físico y UI visual → sesión de dispositivo del dueño (criterios 3-4 en vivo).
- **C (auto-aprobar dificultad ≤3):** no implementada, como decía el plan — solo si las familias reportan fatiga de votar trivialidades.
