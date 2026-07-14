# T18 — Purga de código muerto y vestigios de fases anteriores

**Estado:** ✅ hecho (2026-07-02, opción A) · **Esfuerzo global:** S (una tarde de borrado) · **Depende de:** T9

## El quiebre (por qué)

Cada fase superada dejó fantasmas: campos que nadie lee, DTOs con propiedades que el server ignora desde el JWT, métodos que nunca se llaman. El código muerto no es gratis — cada lector futuro (incluido el propio dev en 3 meses, o la IA de turno) gasta atención en descartar lo que parece importante y no lo es, y algunos fantasmas mienten activamente (un `UserId` que viaja en cada request sugiere que el server lo usa; no). La purga es la mejora con mejor ratio claridad/esfuerzo del repo.

## Evidencia e inventario (verificado con grep en el repo actual)

| # | Vestigio | Dónde | Qué es |
|---|----------|-------|--------|
| 1 | `Pet.UnlockedSkins` | `Pet.cs:38`, mapping `AppDbContext.cs:42`, columna en BD | Nunca leído ni escrito; el inventario real es `User.Inventory` |
| 2 | `Pet.AddGold()` | `Pet.cs:154-158` | Nunca llamado; el código usa `GoldCoins +=` directo (y pierde el clamp a 0 que este método tenía) |
| 3 | `SubmitTaskRequest.UserId` | `TasksController.cs:67` + cliente `GameDataService.cs:116` | El server saca el userId del token desde Fase 5; el campo viaja y se ignora |
| 4 | `BuyRequest.UserId` + `Price` del cliente | `ShopController.cs:170` + `GameDataService.cs:348-353` | Ídem; `Price` es el fantasma del exploit de precio ya cerrado — el server jamás lo lee |
| 5 | `CreateUserRequest` | `UsersController.cs:193-197` | Clase sin ningún uso |
| 6 | `Pet.Happiness` | `Pet.cs:19` + barra en Dashboard | Ya planificado en **T5** (no duplicar aquí; coordinar) |
| 7 | `EmotionalSupportService.GenerateFeedbackAsync` | `EmotionalSupportService.cs:28-75` | Queda muerto tras **T12** (la fusión); `GenerateRevivalEncouragementAsync` decide su destino |
| 8 | `NotificationService` stub | `Services/NotificationService.cs` | `Debug.WriteLine`; **T2** lo reemplaza — si T2 tarda, al menos el `ScheduleNotification` engañoso |
| 9 | `src/test_loop.ps1` | trackeado en git | Script de prueba local |
| 10 | Restos "SQLite"/`pet_prod.db` | comentarios sueltos + archivo untracked | Ya declarados basura en CLAUDE.md |

## Opciones

### A. Purga completa en una pasada (recomendada)
Un solo commit `chore: purga de código muerto` que recorra la tabla: borrar 1-5 y 9 ya; 6-8 se anotan como "muere con T5/T12/T2" en sus planes (no tocar aquí para no chocar). Método: por cada ítem, `grep` de usos antes de borrar (el veredicto de arriba ya lo hizo, re-verificar al ejecutar por si algo cambió), compilar, y el recorrido de humo en emulador al final.
- **Pros:** un commit revisable y revertible; la tabla es el checklist; después de esto, todo lo que queda en el repo *significa algo*.
- **Contras:** el ítem 1 implica decidir sobre la columna en BD (ver abajo); el 3-4 tocan el contrato cliente-server (coordinar con T17 si van cerca).
- **Esfuerzo:** S.

### B. Purga oportunista (cada fantasma muere cuando se toca su archivo)
Regla: quien abra un archivo por otra tarea, se lleva sus muertos.
- **Pros:** cero esfuerzo dedicado.
- **Contras:** en la práctica "oportunista" = "nunca" para los archivos que ninguna tarea abre (`CreateUserRequest` viviría para siempre); mezcla borrados con features en los diffs.
- **Esfuerzo:** 0 dedicado · Aceptable solo para los ítems 6-8 (que ya tienen tarea dueña).

### C. Marcar sin borrar (`[Obsolete]`/comentarios)
- **Pros:** reversible sin git.
- **Contras:** git YA es la reversibilidad; marcar añade ruido en vez de quitarlo. Descartable.

### Sub-decisión: la columna `UnlockedSkins` en BD
- **a — Quitar del modelo + migración de drop:** limpio del todo; migración destructiva pero de una columna que jamás tuvo datos reales (verificar con un `SELECT` antes: debe estar en `[]` para todos).
- **b — Quitar del modelo, dejar la columna huérfana:** cero riesgo, deuda mínima en BD.
- **Recomendada:** **a** con la verificación previa; si diera cualquier duda, b.

### Sub-decisión: quitar `UserId` de los DTOs de request
Borrar la propiedad server-side es seguro (el binder ignora campos extra del JSON del cliente viejo). Orden correcto: primero server (deja de declararla), después cliente (deja de mandarla) — nunca al revés no aplica aquí porque el server ya no la usa, pero mantener el orden evita confusión en diffs.

## Recomendación

**A** para los ítems 1-5 y 9 (con las dos sub-decisiones resueltas como se indica), **B** para 6-8 (tienen tarea dueña: T5, T12, T2). El ítem 2 tiene una alternativa a borrar: *usar* `AddGold` en los dos sitios que hacen `GoldCoins +=`/`-=` y ganar el clamp a 0 — elegir borrar o usar, pero no dejarlo muerto.

## Criterios de éxito / verificación

1. `grep -r "UnlockedSkins\|AddGold\|CreateUserRequest" src/` → solo migraciones históricas (que no se tocan).
2. Los requests de tarea y compra ya no llevan `UserId`/`Price` (verificar el JSON en el log del server en dev) y todo sigue funcionando con el flujo completo en emulador.
3. `git ls-files | grep test_loop` → vacío.
4. El server compila sin warnings nuevos y los 26 tests siguen verdes.
5. (Si 1a) La migración de drop aplicada en local; producción la recibe con el siguiente deploy.

## Dependencias

- **T9** primero. Coordinar con **T17** (ítems 3-4 tocan los mismos DTOs — si T17 va primero, estos mueren ahí).
- Los ítems 6, 7, 8 pertenecen a **T5**, **T12** y **T2** respectivamente.

## Resultado (2026-07-02) — opción A, ítems 1-5, 7, 9, 10

1. `Pet.UnlockedSkins` borrado (prop + mapping/ValueComparer) con **migración `DropPetUnlockedSkins`** (sub-decisión a; verificado antes en BD: 106 filas, 0 con datos; no existe tabla SharedPets — TPH en Pets). Render la aplica al desplegar.
2. `Pet.AddGold()` **borrado** (la tienda valida saldo antes de restar bajo el lock; el clamp era peso muerto).
3. `SubmitTaskRequest.UserId` fuera (server) y el cliente ya no lo manda.
4. `BuyRequest.UserId` fuera; el server nunca declaraba `Price` — el cliente dejó de mandar ambos (solo viaja `ItemName`).
5. `CreateUserRequest` borrado.
7. **`EmotionalSupportService` borrado entero** + registro DI (muerto tras T12; `GenerateRevivalEncouragementAsync` tampoco tenía llamadores).
9. `src/test_loop.ps1` des-trackeado.
10. Sin restos "SQLite"/pet_prod en `src/**/*.cs` (ya estaban limpios).
- Quedan con tarea dueña: ítem 6 `Happiness` (**T5**) e ítem 8 `NotificationService` stub (**T2**).
- Verificación: grep limpio fuera de `Migrations/`; server + cliente (net10.0-windows) compilan; **34 tests verdes**; smoke tarea+compra contra Render tras el deploy ✓ (tarea premiada + compra `success:true` con los DTOs adelgazados; migración de drop aplicada por Render al arrancar). El smoke destapó de paso la **tienda vacía en prod** (Dockerfile sin `Catalog/`) — arreglada en `a65333d`, ver T21.
