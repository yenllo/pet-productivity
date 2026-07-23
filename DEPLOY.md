# Despliegue — PetProductivity

El **servidor** se despliega en **Heroku** (stack `container`, `heroku.yml` → Dockerfile) desde la rama `main`
(autodeploy on push, conectado por GitHub). La **base de datos** es PostgreSQL en Supabase. Ningún secreto
vive en el repo: en local se usan **user-secrets**, en Heroku **config vars** (`heroku config:set`).

> Migrado de Render a Heroku el 2026-07-22/23. Render queda **decomisionado** — no lo asumas como
> fuente de verdad en ningún doc o script nuevo. Motivo del fix que desbloqueó la migración:
> [[heroku-migracion-dyno-crash]] (el ENTRYPOINT `sh -c` se rompía porque Heroku re-envuelve el
> comando del dyno en su propio `sh -c`).

> En .NET el separador de config `:` se escribe como **doble guion bajo** `__` en una env var.
> Ej.: `Jwt:Key` → `Jwt__Key`.

## Variables de entorno requeridas en Heroku

| Variable (Heroku) | Clave config | Para qué | Estado |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | Postgres/Supabase | ✅ requerida (el server no arranca sin esto) |
| `Gemini__ApiKey` | `Gemini:ApiKey` | Tasador IA (Gemini) | ✅ requerida |
| `Google__ClientId` | `Google:ClientId` | Login con Google | login Google |
| `Google__ClientSecret` | `Google:ClientSecret` | Login con Google | login Google |
| `Google__RedirectUri` | `Google:RedirectUri` | `https://pet-productivity-c03ac5654dd2.herokuapp.com/api/auth/google/callback` | login Google |
| `Google__AppCallback` | `Google:AppCallback` | `petproductivity://auth` | login Google |
| `Jwt__Key` | `Jwt:Key` | Firmado de tokens de sesión (rotada en el cutover, 2026-07-22) | ✅ requerida |
| `Jwt__Issuer` | `Jwt:Issuer` | Emisor de tokens (`petproductivity`) | ✅ requerida |
| `Firebase__ProjectId` | `Firebase:ProjectId` | Envío de push (FCM) | ✅ requerida |
| `Firebase__ServiceAccountJson` | `Firebase:ServiceAccountJson` | Credencial del servidor para FCM (contenido completo del JSON, **no** un path — un `Firebase__ServiceAccountPath` no sirve en Heroku, no hay filesystem persistente) | ✅ requerida |
| `Sentry__Dsn` | `Sentry:Dsn` | Observabilidad (T15) | opcional, sin ella el SDK queda apagado |

## Notas
- **Google Cloud Console** → el OAuth client debe tener registrado el redirect URI exacto:
  `https://pet-productivity-c03ac5654dd2.herokuapp.com/api/auth/google/callback`
  (hay que agregarlo a mano en Credentials — Heroku no lo hace por vos).
- **Firebase**: el service account JSON no se commitea. En Heroku se pasa su contenido completo como
  el valor de `Firebase__ServiceAccountJson` (config var), no como archivo — `PushService` lee esa
  variable primero y cae a `Firebase:ServiceAccountPath` (archivo local) solo si está vacía.
- **`google-services.json`** (app Android): va en `src/PetProductivity.Client/Platforms/Android/`, **gitignored**. Cada quien usa el suyo.
- El cliente apunta por defecto a Heroku (`Constants.BaseUrl`). Para desarrollo local contra el emulador: Ajustes → Dirección del Servidor → `http://10.0.2.2:5051`.

## Observabilidad (T15)
- **Sentry (opcional, tier gratis):** crear proyecto *ASP.NET Core* en sentry.io y poner la env var
  **`Sentry__Dsn`** en Render. Sin la variable, el SDK queda apagado (no-op). `SendDefaultPii=false`
  ya viene fijado en código (no viajan usuario/IP).
- **Sentry en el cliente Android (crashes de teléfonos reales):** crear un **segundo** proyecto en
  sentry.io de tipo *Android* (o *.NET MAUI*) y exportar su DSN como variable de entorno
  **`SENTRY_DSN_MAUI`** *antes de compilar el APK* — el `.csproj` la inyecta en el ensamblado. **No se
  versiona a propósito:** el repo es público y un DSN hardcodeado haría que los crashes de cualquiera
  que compile el código caigan en tu dashboard. Sin la variable, Sentry queda apagado (no-op), que es
  el default correcto para quien clone el repo.
  ```powershell
  $env:SENTRY_DSN_MAUI = "https://...@...ingest.sentry.io/..."   # antes del build de release
  ```
- **UptimeRobot (gratis, ~15 min):** uptimerobot.com → *Add New Monitor* → tipo **HTTP(s)** →
  URL `https://pet-productivity-c03ac5654dd2.herokuapp.com/health` → intervalo **5 min** → alerta al email.
  **Pendiente:** el monitor viejo apuntaba a Render — reapuntarlo o crear uno nuevo para Heroku.
- **CI:** `.github/workflows/ci.yml` corre los tests del server en cada push a `main` (el cliente MAUI
  queda fuera a propósito). Heroku no espera al CI: el rojo avisa, no bloquea.

## Verificar un deploy
- `GET https://pet-productivity-c03ac5654dd2.herokuapp.com/api/shop/catalog` → 200 **y con contenido** (un `[]` = imagen sin `Catalog/`).
- Las migraciones EF se aplican solas al arrancar (`db.Database.Migrate()`).
