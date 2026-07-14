# T22 — Análisis completo de seguridad (chat nuevo dedicado)

**Estado:** pendiente · **Tipo:** revisión · **Orden:** LO SEGUNDO, inmediatamente después de T9 (commit) · **Esfuerzo:** L

## Por qué es especial

Es la única revisión que puede contener algo **urgente de verdad** (explotable en producción), y merece atención sin distracciones. Por eso NO se hace en la conversación de planificación: se abre un **chat completamente nuevo, solo Fable 5 a max effort**, con el prompt de abajo. Se hace **después del commit (T9)** para que el revisor lea un working tree limpio y sus hallazgos se puedan arreglar sobre una base commiteada.

## Cómo lanzarlo

1. Cerrar/dejar esta conversación.
2. Abrir un chat nuevo de Claude Code en `C:\Renzo\Proyectos\PetProductivity`.
3. Modelo **Fable 5**, effort **max** (`/effort` → max).
4. Pegar el prompt de la sección siguiente tal cual.

## Contexto que el revisor debe conocer (ya incluido en el prompt)

- Stack: ASP.NET Core + EF Core (server), .NET MAUI Android (cliente), Postgres/Supabase, Gemini (IA), FCM (push), Render (deploy). Auth = JWT HS256 (userId en `sub`); hub SignalR `[Authorize]` por `access_token` en query.
- Ya se sabe (NO son hallazgos nuevos): login acepta contraseñas legadas en texto plano como fallback (`UsersController.cs:62-72`, ya planificado en T11-D4); secretos fuera del repo vía user-secrets y rotados; rate-limit "ai" 10/min en `/api/tasks`.

---

## PROMPT PARA PEGAR EN EL CHAT NUEVO

```
Eres un auditor de seguridad. Quiero una revisión de seguridad COMPLETA y sistemática
de esta app, en español, priorizada por severidad. Trabaja con calma y a fondo: lee el
código real antes de afirmar nada y cita archivo:línea en cada hallazgo.

## La app
PetProductivity: app de productividad gamificada. Cliente .NET MAUI (Android), servidor
ASP.NET Core + EF Core, base de datos PostgreSQL en Supabase, IA de juicio con Google
Gemini (server-side), push con Firebase FCM, deploy en Render. Código bajo src/:
PetProductivity.Server (API + hubs SignalR), PetProductivity.Client (MAUI),
PetProductivity.Shared (modelos).

## Autenticación (contexto)
Auth de sesión = JWT HS256, userId en el claim `sub`. Todos los controllers y el
FamilyHub van [Authorize]; el userId sale del TOKEN, no del body. SignalR recibe el
token por query string `access_token`. Login con Google = OAuth mediado por el servidor
(AuthController /api/auth/google/start + /callback, redirige a la app con ?token=).
Registro diferido: usuario invitado primero, luego "reclama" la cuenta con UpgradeAccount.

## Ya conocido — NO lo reportes como hallazgo nuevo (pero sí evalúa si hay algo PEOR alrededor)
- El login tiene un fallback que acepta contraseñas legadas en texto plano
  (UsersController.cs, método Login) — ya está planificado eliminarlo.
- Los secretos están fuera del repo (user-secrets) y fueron rotados; appsettings.json
  solo tiene placeholders. El historial de git tiene claves viejas ya inservibles.
- Hay rate-limit de 10/min en el endpoint de IA (/api/tasks).

## Qué quiero que revises (exhaustivo, no te limites a esta lista)
1. AUTORIZACIÓN / IDOR: recorre CADA endpoint de cada controller (Auth, Users, Tasks,
   Focus, Shop, Groups, Dev) y verifica que un usuario no pueda leer/mutar recursos de
   otro. Presta atención a los que reciben un id en la ruta o el body. ¿El FamilyHub
   valida pertenencia al grupo en cada método? ¿GetProof (Focus) filtra bien por dueño
   o compañero de grupo?
2. AUTENTICACIÓN: expiración y validación del JWT (¿caduca? ¿se valida issuer/audience/
   lifetime? ¿el secreto tiene entropía suficiente y viene de config, no hardcodeado?).
   Flujo OAuth de Google: ¿el callback valida el state (CSRF)? ¿el token en la URL de
   redirección se puede interceptar/loguear? Reclamo de cuenta invitada: ¿se puede
   secuestrar o reclamar dos veces? Enumeración de usuarios: ¿login/registro dan
   mensajes distintos para email existente vs. inexistente? ¿Hay rate-limit de
   login/registro contra fuerza bruta (hoy solo /api/tasks tiene "ai")?
3. VALIDACIÓN DE ENTRADA / DoS: límites de tamaño del body, especialmente
   FocusProofRequest.ImageBase64 (¿un base64 de 100 MB tumba el server o agota memoria?).
   ¿El rate-limit cubre /api/focus/proof, que también gasta cuota de Gemini? ¿Otros
   endpoints caros sin límite? Payloads de placements (¿tope real?).
4. INYECCIÓN: uso de EF (¿todo parametrizado, algún Raw SQL?), y sobre todo PROMPT
   INJECTION hacia Gemini — el texto del usuario y las fotos van a la IA; ¿el prompt
   del juicio está blindado (ya usa tags <task> como "untrusted")? OJO ESPECÍFICO: el
   prompt de visión en FocusController.Proof interpola r.Description SIN tag de
   untrusted — evalúa si es explotable. ¿La respuesta de la IA se valida antes de
   convertirla en recompensa/estado?
5. EXPOSICIÓN DE DATOS: ¿alguna respuesta filtra el hash de contraseña, el DeviceToken,
   emails de otros, o datos internos? (revisa las serializaciones y los objetos
   anónimos que devuelven los controllers). Logs: ¿se loguean secretos, tokens,
   contraseñas o PII (descripciones de tareas, fotos)?
6. SECRETOS Y CONFIG: fail-fast si falta config crítica; ¿DevController está realmente
   gateado a IsDevelopment? ¿CORS, headers de seguridad, HTTPS forzado? ¿El endpoint de
   validación de recibo premium se puede engañar?
7. ALMACENAMIENTO / PRIVACIDAD: las fotos de comprobante se guardan en la BD (bytea) 30
   días y se mandan a Gemini. ¿Control de acceso correcto al servirlas? ¿Retención y
   borrado implementados como se dice?
8. CLIENTE (MAUI/Android): almacenamiento del token (Preferences: ¿cifrado?
   ¿SecureStorage?), el AndroidManifest (permisos excesivos, exported components,
   deep-link/WebAuthenticationCallbackActivity mal configurado), y cualquier secreto
   embebido en el APK.
9. DEPENDENCIAS: paquetes con vulnerabilidades conocidas (dotnet list package
   --vulnerable) y versiones desactualizadas de algo sensible.

## Formato de salida
Una lista priorizada (Crítico / Alto / Medio / Bajo). Por hallazgo:
- Título + severidad
- Archivo:línea
- Cómo se explota (escenario concreto)
- Impacto
- Fix recomendado (concreto)
Separa lo CONFIRMADO (leíste el código y lo verificaste) de lo SOSPECHOSO (merece
verificación). Si algo está bien hecho, dilo brevemente al final (para no re-revisarlo).
No apliques cambios: es solo el informe. Cuando termines, guarda el informe en
tareas/22-seguridad-INFORME.md.
```

---

## Al volver con el informe

- Los hallazgos Crítico/Alto se arreglan de inmediato (pueden generar sus propios mini-planes en `tareas/`).
- Cruzar con T24 (auth interna) y T25 (Android): comparten superficie; los dos ángulos deberían coincidir.
- Actualizar el Estado de T22 aquí y en `00-indice.md`.

## Dependencias

- **T9 (commit) primero** — es literalmente lo segundo a hacer. Alimenta y se cruza con T14 (privacidad), T24 (auth), T25 (Android).
