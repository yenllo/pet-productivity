# T21 — Verificación dinámica (reproducir antes de arreglar)

**Estado:** ✅ hecho (2026-07-02, junto a T11) · **Tipo:** revisión · **Esfuerzo:** M · **Depende de:** T9 · **Se hace junto a:** T11

## Por qué esta revisión

Los tres análisis (T1-T20) fueron **lectura estática**: ningún hallazgo se reprodujo ejecutando la app. "Confirmado por lectura" es un grado de certeza menor que ver el fallo. Antes de arreglar los defectos de T11, hay que reproducirlos en un server local + emulador — para validar que existen (y no malinterpreté el código) y para tener el "antes" contra el que verificar el "después". De paso, correr la app en vivo destapa la clase de bugs que la lectura no ve: timing, reconexión, UI, estados intermedios.

## Qué revisar (checklist)

1. **Doble premio de aprobación (T11-D2):** dos `POST /api/groups/.../approve` casi simultáneos sobre la misma `TaskApproval` → ¿la recompensa se aplica dos veces? (dos emuladores o dos requests con el mismo token).
2. **Foco pierde recompensa (T11-D1):** forzar excepción en `ProcessTaskCompletion` (throw temporal en dev) tras completar un foco → ¿se perdió la sesión y el tiempo?
3. **Decadencia tras reinicio (T10):** poner `LastDecayAt`/estado, reiniciar el server local, observar si los ticks perdidos se recuperan o se saltan.
4. **TaskId falso (T11-D3):** ver el `TaskId` de la respuesta de `/api/tasks` y comprobar que no existe en `/api/focus/history`.
5. **Reconexión SignalR:** iniciar Frenesí, cortar red del cliente, reconectar → ¿re-suscribe grupos, se pierden eventos, se duplican?
6. **Cold start de Render:** medir el tiempo real de la primera request tras inactividad (alimenta T13-D).
7. Anotar cualquier bug incidental que aparezca al usar la app (crashes, estados raros).

## Cómo (setup)

- Server local: `dotnet run --project src/PetProductivity.Server --launch-profile http` → `http://0.0.0.0:5051` (hace `Migrate()` al arrancar).
- Emulador sin crash de GPU: `emulator -avd medium_phone -no-snapshot-load -gpu swiftshader_indirect`.
- Apuntar el cliente: Ajustes → Dirección del Servidor → `http://10.0.2.2:5051` → reiniciar app.
- Logs: `adb logcat -s ANTIGRAVITY ANTIGRAVITY_CRASH`; crash a archivo: `adb shell run-as ... cat files/crash.txt`.
- `DevController` (gateado a Development) para acelerar decadencia/estados.

## Salida esperada

Una lista de defectos **reproducidos** (con los pasos exactos) separada de los **no reproducibles** (que entonces se re-analizan o se cierran en su plan). Los reproducidos entran a T11 con su "antes" documentado.

## Dependencias

- **T9** primero. Se ejecuta **entrelazada con T11** (reproducir → arreglar → re-verificar en la misma sesión). Alimenta T10 y T13.

## Resultado (2026-07-02) — repro con script REST (usuarios `t21_*`) contra server local + Supabase compartida

**Reproducidos y luego arreglados (T11):**
1. **D1 ✓ reproducido:** throw simulado dentro de `ProcessTaskCompletion` al completar un foco → 500 y el retry dio 404 "Sesión no encontrada" (tiempo perdido). Tras el fix: 500 → retry **200 con recompensa**.
2. **D3 ✓ reproducido:** `POST /api/tasks` devolvió `taskId` inexistente en `/api/focus/history`. Tras el fix: el id devuelto **es** el del historial.
3. **D2-bis (hallazgo nuevo) ✓ reproducido — voto perdido:** grupo de 3 (`needed=2`), dos votantes distintos concurrentes → ambos 200 pero `votes=1` final (last-write-wins en el JSON `Approvals`); la tarea quedaba pendiente pese a tener los 2 votos. Tras el fix (mismo lock de D2): `votes:1` → `votes:2, approved:true`.

**No reproducido (con matiz importante):**
4. **D2 doble premio:** la ventana concurrente SÍ se abre (7 de 8 double-taps entraron a la carrera) pero el premio doble lo frena un guard **accidental**: el voto muta la columna JSON y el `UPDATE` sobre la fila ya borrada lanza `DbUpdateConcurrencyException` revirtiendo el premio del perdedor en el mismo `SaveChanges`. Manifestación real del race: **7×500** al usuario. Tras el fix: 404 benignos y premio exactamente 1 vez (verificado 4 intentos + test xUnit concurrente).

**Cerrados por inspección / medición:**
5. **Decadencia tras reinicio (→T10):** `HealthDecayHostedService` es un `Task.Delay(2h)` sin estado persistido — no existe `LastDecayAt`; los ticks perdidos por downtime se saltan en silencio y el primer tick llega recién a las 2 h de uptime. Confirmado por código (nada que reproducir).
6. **Cold start de Render (→T13-D):** **22.7 s** la primera request tras ~1.5 h de inactividad; **0.28 s** en caliente.
7. **Reconexión SignalR:** diferida a **T25** (decisión del dueño en esta sesión).

**Bug incidental (ítem 7 del checklist), encontrado por el smoke de T18 contra producción:** la tienda de Render estaba **VACÍA** (`/api/shop/catalog` → `[]`) desde el deploy del diorama de esa mañana — el Dockerfile solo copiaba `src/` y el catálogo vive en `Catalog/` (raíz). Arreglado y verificado el mismo día (commit `a65333d`): 189 ítems de vuelta y compra real 200 en prod. Moraleja anotada: el smoke post-deploy debe incluir **contenido**, no solo códigos HTTP (el poll de cold-start vio 200 con cuerpo vacío y no lo distinguió).
