# T23 — Rendimiento y base de datos

**Estado:** ✅ hecho (2026-07-02) · **Tipo:** revisión · **Esfuerzo:** M · **Depende de:** T9

## Por qué esta revisión

Nunca se miraron índices, planes de consulta ni el schema como tal. Hay patrones que a 10 usuarios son invisibles y a 500 duelen. No es urgente hoy (la base de usuarios es mínima), pero es barato de revisar ahora y caro de descubrir en producción bajo carga.

## Qué revisar (checklist)

1. **Dedupe cargando todo en memoria:** `PetService.cs:136-139` trae TODAS las descripciones de las últimas 24 h del usuario y las normaliza en C# por cada tarea. Con historial grande, es O(n) por submit. ¿Mover la normalización a una columna indexada, o al menos limitar/`Any()` en SQL?
2. **Índices de las consultas calientes:** ¿existe índice en `TaskItems(UserId, CreatedAt)`? Es la consulta más frecuente (dedupe, rendimientos decrecientes, historial). Ídem `FocusSessions(UserId)`, `GroupMemberships(GroupId, UserId)`, `TaskApprovals(GroupId)`.
3. **Tick de decadencia trae todo a memoria:** `HealthDecayHostedService.cs:47-50` — `.ToListAsync()` de todas las mascotas por tick. (T10 lo vuelve lazy; verificar que el barrido residual filtre por actividad reciente.)
4. **Cascadas de borrado:** al borrar un usuario/grupo/mascota, ¿qué pasa con `TaskItems`, `FocusProofs`, `GroupMemberships`, `TaskApprovals`? (relevante para T14-C1c, borrado de cuenta). Revisar `OnDelete` en `AppDbContext`.
5. **Tipos y la migración pendiente:** revisar el schema real en Supabase vs. el modelo; confirmar que la migración sin aplicar (working tree) es aditiva y no rompe datos.
6. **Fotos en la tabla:** `FocusProof.Image` como `bytea` en la BD principal — ¿tamaño, impacto en backups/consultas? ¿Debería ir a blob storage? (cruza con T14-privacidad).
7. **N+1 en la capa social:** revisar `GroupService` y `GroupDetailDto` — ¿arma el detalle del grupo con una query o con una por miembro?

## Cómo

- Habilitar logging de SQL de EF Core en Development (`LogTo` / `EnableSensitiveDataLogging`) y mirar las queries que se emiten en el flujo real.
- Inspeccionar índices en Supabase (SQL editor: `\d tabla` equivalente / `pg_indexes`).
- No optimizar a ciegas: medir primero, cambiar solo lo que el log muestre repetido o caro.

## Salida esperada

Lista priorizada de índices a añadir (migración) y de consultas a reescribir, con el "antes" del log de EF. Los ítems que solo importan a escala se anotan como "diferido hasta N usuarios".

## Dependencias

- **T9** primero. #4 alimenta **T14** (borrado de cuenta); #6 cruza con **T14**-privacidad; #3 con **T10**.

## Resultado (2026-07-02) — radiografía real de Supabase (pg_indexes) + migración `AddHotPathIndexes`

**Estado de la BD:** 87 usuarios, 106 pets, 43 TaskItems — todo diminuto; momento barato para indexar. `GroupMemberships(GroupId,UserId)` e `InviteCode` ya estaban únicos ✓; **TaskItems/FocusSessions/TaskApprovals/JoinRequests/FocusProofs solo tenían PK** y `Users.Email` no tenía índice.

1. **(#2) Índices añadidos (migración, 9):** `TaskItems(UserId,CreatedAt)` + `(PetId,CreatedAt)` + `(ProofId)` · `TaskApprovals(GroupId)` · `JoinRequests(GroupId,RequesterUserId)` **UNIQUE** (de paso cierra la doble solicitud concurrente) · `FocusSessions(UserId,PetId)` · `FocusProofs(SessionId,UserId)` · `GroupFocusSessions(GroupId)` · `Users(Email)` **UNIQUE** (verificado 0 duplicados; de paso cierra la carrera del registro — un choque rarísimo dará 500 en vez de cuenta duplicada). Render la aplica al desplegar.
2. **(#1) Dedupe en memoria:** trae las descripciones de 24 h y normaliza en C#. Con el índice nuevo el fetch es barato y el volumen diario está acotado por los rendimientos decrecientes → **diferido hasta ~500 usuarios** (entonces: columna normalizada indexada).
3. **(#3) Tick de decadencia a memoria:** confirmado (todas las mascotas por tick); es exactamente lo que **T10** rediseña — sin cambio aquí.
4. **(#4) Cascadas: NO HAY.** Solo 2 FKs reales (User→Pet, Group→SharedPet). `TaskItems/FocusProofs/GroupMemberships/TaskApprovals/JoinRequests` usan Guids sueltos sin FK → **el borrado de cuenta (T14-C1c) debe limpiar a mano** cada tabla o dejará huérfanos. Los flujos actuales (LeaveGroup) ya limpian manualmente (reforzado en T24).
5. **(#5) Migración pendiente:** obsoleto — T9/T18/T23 ya aplicaron todo (el esquema vivo = modelo).
6. **(#6) Fotos bytea:** 3 fotos, 84 kB total, ~28 kB c/u (comprimidas a 512px client-side) + limpieza a 30 días → **bytea está bien; blob storage diferido** hasta que el tamaño duela.
7. **(#7) N+1 social: NO HAY.** `GetGroupDetailAsync` arma todo con ~6 queries acotadas (memberships, names-dictionary, requests+names, approvals, presencia en memoria) — sin query-por-miembro.
- *Método: no se capturó log SQL de EF (a esta escala no aporta sobre pg_indexes + lectura de las queries); medir con log cuando haya carga real.*
