# T11 — Bucle central tolerante a fallos (4 defectos concretos)

**Estado:** ✅ hecho (2026-07-02, junto a T21) · **Esfuerzo global:** S-M · **Depende de:** T9

## El quiebre (por qué)

El camino feliz del bucle tarea→recompensa funciona (verificado en producción), pero sus bordes pierden datos o duplican recompensas. En una app cuyo producto es "tu esfuerzo queda registrado y premiado", perder el esfuerzo de un usuario UNA vez destruye más confianza que cien sesiones correctas. Son cuatro defectos independientes; se agrupan porque comparten causa (orden de operaciones y concurrencia) y se arreglan en la misma pasada.

## Evidencia y opciones por defecto

### D1. El foco borra la sesión ANTES de premiar → excepción = 60 min perdidos
`src/PetProductivity.Server/Controllers/FocusController.cs:69-83`: `Remove(session)` + `SaveChangesAsync()` ocurren **antes** de `ProcessTaskCompletion`. Si esa llamada lanza (bug, BD, timeout), la sesión ya no existe: el usuario no puede reintentar y su tiempo cronometrado se esfumó.

- **Opción a — invertir el orden:** premiar primero, borrar después. Riesgo inverso: si el borrado falla tras premiar, la sesión queda viva y se podría completar dos veces → mitigar marcando/verificando dentro del mismo `SaveChanges` final.
- **Opción b — transacción:** envolver premio+borrado en `_db.Database.BeginTransactionAsync()`. Nota: `ProcessTaskCompletion` usa el mismo `AppDbContext` scoped, así que la transacción los cubre; el `PetWriteLock` interno no conflictúa.
- **Opción c — soft-close:** marcar `CompletedAt` en vez de borrar; el cleanup ya existente la purga después. Idempotencia gratis (una sesión completada no se completa dos veces) y deja rastro para depurar.
- **Recomendada:** **b** (transacción) — cambio mínimo con garantía real; **c** si además se quiere auditar. **Esfuerzo:** S.

### D2. Aprobación concurrente puede premiar doble
`src/PetProductivity.Server/Services/PetService.cs:258-292` (`ApproveTaskAsync`): dos miembros aprobando a la vez leen la misma fila, ambos computan `approved=true`, ambos llaman `ApplyRewardAsync` (el `PetWriteLock` serializa los writes pero no evita que se apliquen dos veces), y el segundo `Remove` es el único que "pierde".

- **Opción a — lock por aprobación:** reutilizar `PetWriteLock` con `approvalId` como clave (es un lock genérico por Guid) envolviendo leer→votar→aplicar→borrar. Simple, mismo patrón del repo.
- **Opción b — borrado como test-and-set:** ejecutar el `Remove` + `SaveChanges` ANTES de aplicar la recompensa y capturar `DbUpdateConcurrencyException` — solo quien borró la fila premia. Sin locks, pero reordena la lógica.
- **Opción c — token de concurrencia EF** (`xmin` de Postgres) en `TaskApproval`. Correcto pero más ceremonia para una tabla efímera.
- **Recomendada:** **a** — 3 líneas con infraestructura existente. Nota: con la auto-aprobación de T6, el tick del cleanup se vuelve otro "aprobador" concurrente → el lock debe cubrirlo también. **Esfuerzo:** S.

### D3. `TaskId` de la respuesta es un Guid inventado
`src/PetProductivity.Server/Services/PetService.cs:198`: `TaskId = Guid.NewGuid()` — no es el Id del `TaskItem` real creado en `ApplyRewardAsync`. Cualquier feature futura que use ese Id (deshacer, detalle, comprobante) apuntará a la nada.

- **Opción a — devolver el Id real:** `ApplyRewardAsync` crea el `TaskItem`; devolverlo (o su Id) y propagarlo al `TaskResult`.
- **Opción b — eliminar el campo** si nadie lo consume (verificar cliente; hoy `TasksController.cs:50` lo serializa).
- **Recomendada:** **a** (es un `return` extra); b solo si al revisar el cliente resulta muerto. **Esfuerzo:** S.

### D4. Login acepta contraseñas legadas en texto plano
`src/PetProductivity.Server/Controllers/UsersController.cs:62-72`: si el hash no verifica, se compara contra texto plano y se re-hashea al vuelo. Puente de migración razonable en su momento; hoy es una rama de código que compara contraseñas en claro y que vivirá para siempre si nadie le pone fecha.

- **Opción a — borrar el fallback ya:** si la BD ya no tiene filas en texto plano (verificable: los hashes de ASP.NET Identity empiezan con `AQAAAA` en base64), es borrar el bloque.
- **Opción b — migración forzada:** marcar las cuentas legadas (si existen) para reset de contraseña y borrar el fallback.
- **Opción c — fecha de defunción:** dejarlo N semanas más con un log de advertencia cuando se use, y borrarlo si el log nunca aparece.
- **Recomendada:** **a** previa consulta a la BD; si hay legados, **c** con ventana corta. **Esfuerzo:** S.

## Recomendación global

Una sola pasada: D1-b, D2-a, D3-a, D4-a — los cuatro son cambios pequeños de server sin migraciones (salvo D1-c si se eligiera). Hacerla antes de T6 (auto-aprobación) porque T6 multiplica los caminos concurrentes de D2.

## Criterios de éxito / verificación

1. (D1) Forzar excepción dentro de `ProcessTaskCompletion` (throw temporal en dev) al completar un foco → la sesión sigue existiendo y el retry funciona.
2. (D2) Test xUnit: dos `ApproveTaskAsync` concurrentes sobre la misma aprobación → la recompensa se aplica exactamente una vez (ya existe `ApproveTaskTests.cs` como base).
3. (D3) La respuesta de `POST /api/tasks` trae un `TaskId` que existe en `/api/focus/history`.
4. (D4) Login con usuario normal sigue funcionando; no queda ninguna comparación de contraseña en claro en el repo (grep).

## Dependencias

- **T9** primero. **T6** (auto-aprobación) debe construirse sobre el D2 arreglado.

## Resultado (2026-07-02) — las 4 opciones recomendadas, verificadas en vivo (ver T21)

- **D1-b:** `FocusController.Complete` envuelto en `BeginTransactionAsync` (borrar sesión + premio + racha = todo o nada). Verificado: excepción simulada → la sesión sobrevive y el retry premia. *Nota: la transacción abarca la llamada a la IA (segundos); tolerable a esta escala, se acorta al extraer FocusService (T17-E).*
- **D2-a:** `ApproveTaskAsync` serializado con `PetWriteLock` por `approvalId` (leer→votar→premiar→borrar). Arregla también el **voto perdido** (variante descubierta en T21) y los 500 por double-tap (ahora 404 benigno). Test nuevo: `ApproveTask_VotosConcurrentes_NoSePierdenVotos_NiSePremiaDoble` (suite: 32 verdes).
- **D3-a:** `ApplyRewardAsync` devuelve el `TaskItem`; `TaskResult.TaskId` es el id real (existe en `/api/focus/history`).
- **D4-a:** fallback de texto plano **borrado** tras verificar la BD (consulta read-only autorizada: 68 usuarios, 68 con hash `AQAAAA…`, 0 legados, 0 vacíos).
