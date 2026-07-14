# T13 — Cola offline: el submit de tarea no puede perderse por falta de señal

**Estado:** ✅ hecho (2026-07-03) · **Esfuerzo global:** M · **Depende de:** T9

> **Implementado (A + D + B, como recomendaba el plan):** ping de calentamiento `GET /health` al abrir la app; 1 reintento a los 2 s en submit de tarea y `focus/complete` (duplicado acotado por el dedupe ×0.1 de 24 h del server); cola en `Preferences` (`OfflineSendQueue`, tope 20, TTL 48 h) SOLO para los dos envíos irreemplazables. Drena al abrir la app, al volver la conectividad (MAUI `Connectivity`) y con un re-drenado diferido a 60 s tras encolar (cubre Render dormido CON wifi, donde no hay evento de conectividad). En el drenado: excepción/5xx → conserva y reintenta después; 2xx/4xx → saca de la cola; éxito → notificación local con la recompensa + re-hidratación del estado. **De paso se arregló un bug pre-existente fatal:** `AuthService` borraba el `AuthToken` cuando `/me` fallaba por red (una cuenta Google que abría la app sin señal perdía la sesión y renacía como invitado); ahora solo se descarta con 401/403.
>
> **Verificado en emulador contra server local (los 5 criterios):** (1) modo avión → "📡 Sin conexión: guardada" → volver la red → se envió sola (+27 XP/+13 Oro en el server); (2) matar la app con cola pendiente → reabrir con red → drenada (+20 XP); (3) por código: `FocusMath` premia `min(elapsed, target)` — un complete tardío no se castiga, y las sesiones no expiran server-side; (4) server apagado con wifi → encolada → server arriba → el re-drenado de 60 s la entregó solo (+15 XP); (5) entrada inyectada con 3 días de antigüedad → purgada sin enviarse. C (cola generalizada) descartada como decía el plan.

## El quiebre (por qué)

El cliente es 100% dependiente de red: una tarea descrita en el metro, en el campus sin wifi o con el server de Render en cold start termina en un error… y el texto que el usuario escribió se pierde. El momento más valioso de la app —el usuario contándote lo que logró— es exactamente el que no tiene red de seguridad. No hace falta "modo offline" completo (la arquitectura server-céntrico es una decisión tomada y correcta); hace falta que **el envío** sobreviva a la falta de conectividad.

## Evidencia en el código

- `src/PetProductivity.Client/Services/GameDataService.cs` — los POST van directo a `HttpClient`; una excepción de red sube al ViewModel y el input se descarta.
- Decisión de arquitectura #1 del CLAUDE.md: server = única fuente de verdad, sin JSON local — cualquier solución debe encolar *intenciones*, no duplicar *estado*.
- El cold start de Render (si el plan duerme la instancia) convierte la primera petición del día en un timeout probable — mismo síntoma que ir sin señal.

## Opciones

### A. Retry inmediato con reintentos y timeout generoso (mínima)
Envolver el submit en reintentos (2-3 intentos con backoff corto) y subir el timeout del primer request del día (cold start ≈ 30-60 s). UI: "enviando…" persistente en vez de error inmediato.
- **Pros:** trivial; cubre cold start y micro-cortes (la mayoría de los fallos reales).
- **Contras:** no cubre sin-señal sostenido (metro): si los 3 intentos fallan, se vuelve al problema original salvo que se combine con B/C.
- **Esfuerzo:** S · **Toca:** cliente.

### B. Cola local de envíos pendientes (recomendada)
Si el POST falla por red, la *intención* (descripción, petId, timestamp local) se guarda en `Preferences` (JSON, mismo mecanismo ya usado para `AuthToken`/ajustes) y la UI confirma "guardada, se enviará al reconectar". Al abrir la app / recuperar conectividad (`Connectivity.ConnectivityChanged` de MAUI, ya disponible), se drena la cola en orden y se muestra la recompensa al llegar.
- **Pros:** el texto del usuario nunca se pierde; no viola "server = única fuente de verdad" (no hay estado de juego local, solo un buzón de salida); `Preferences` evita añadir SQLite (prohibido por regla del proyecto) o archivos JSON de estado (mal recuerdo del bug "doble fuente de verdad" — esto es distinto: cola de salida, no caché de estado).
- **Contras:** decisiones de borde — ¿la recompensa diferida se muestra como toast al llegar o queda solo en el historial?; el dedupe server de 24 h usa `CreatedAt` del server, así que una tarea encolada 2 días llega "hoy" (aceptable; opcionalmente mandar el timestamp local como dato informativo); expirar entradas de cola muy viejas (>48 h) para no premiar arqueología.
- **Esfuerzo:** M · **Toca:** cliente (servicio de cola + puntos de submit + un toast).

### C. Cola generalizada para todas las mutaciones
Extender B a compras, equipar estilo, ritual, aprobaciones…
- **Pros:** consistencia total offline.
- **Contras:** la mayoría de esas acciones se hacen mirando estado del server (comprar sin saber tu oro real invita conflictos); el costo/beneficio se desploma fuera del submit de tareas; sobre-ingeniería para el uso real de la app.
- **Esfuerzo:** L · Descartable: solo el submit de tareas (y quizá completar foco) es irreemplazable — el resto el usuario lo reintenta sin dolor.

### D. Ping de calentamiento contra el cold start
Al abrir la app, un `GET /health` en background "despierta" a Render antes de que el usuario termine de escribir su primera tarea.
- **Pros:** 5 líneas; elimina el timeout más frecuente en la práctica.
- **Contras:** paliativo del síntoma de hosting, no de la falta de señal; complementa, no sustituye.
- **Esfuerzo:** S · **Toca:** cliente.

## Recomendación

**A + D ya** (baratas, cubren el 80% de fallos reales) y **B** como la solución de fondo, limitada a: submit de tareas y `focus/complete` (los dos envíos cuyo contenido no se puede reconstruir). **C** no — es la versión sobre-ingenierizada de un problema que solo existe de verdad en dos endpoints. Al implementar B, cola con tope (p. ej. 20 entradas) y expiración a 48 h.

## Criterios de éxito / verificación

1. Modo avión → describir tarea → la app confirma "guardada" (no error) → quitar modo avión → la tarea se envía sola y la recompensa aparece (verificado en emulador con el server local).
2. Matar la app con cola pendiente → al reabrir con red, se drena.
3. Completar un foco sin señal no pierde la sesión (el server la mantiene; el complete encolado llega después y el tiempo lo mide el server igual — verificar que `FocusMath` premie por el target y no castigue el retraso del request; si castiga, es un ajuste de T11/D1).
4. Cold start simulado (server apagado 1 min, encendido): el primer submit del día entra sin intervención del usuario.
5. Una entrada de cola de hace 3 días NO se envía (expiración).

## Dependencias

- **T9** primero. Toca los mismos archivos del working tree (`GameDataService`).
- El punto 3 conecta con **T11-D1** (orden premio/borrado en el foco).
