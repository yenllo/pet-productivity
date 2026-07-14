# Despliegue — PetProductivity

El **servidor** se despliega en Render (Dockerfile) desde la rama `main`. La **base de datos** es PostgreSQL en Supabase. Ningún secreto vive en el repo: en local se usan **user-secrets**, en Render **variables de entorno**.

> En .NET el separador de config `:` se escribe como **doble guion bajo** `__` en una env var.
> Ej.: `Jwt:Key` → `Jwt__Key`.

## Variables de entorno requeridas en Render

| Variable (Render) | Clave config | Para qué | Estado |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | Postgres/Supabase | ✅ requerida (el server no arranca sin esto) |
| `Gemini__ApiKey` | `Gemini:ApiKey` | Tasador IA (Gemini) | ✅ requerida |
| `Google__ClientId` | `Google:ClientId` | Login con Google | login Google |
| `Google__ClientSecret` | `Google:ClientSecret` | Login con Google | login Google |
| `Google__RedirectUri` | `Google:RedirectUri` | `https://petproductivity.onrender.com/api/auth/google/callback` | login Google |
| `Google__AppCallback` | `Google:AppCallback` | `petproductivity://auth` | login Google |
| `Jwt__Key` | `Jwt:Key` | Firmado de tokens de sesión | Fase 5 (auth) |
| `Jwt__Issuer` | `Jwt:Issuer` | Emisor de tokens (`petproductivity`) | Fase 5 (auth) |
| `Firebase__ProjectId` | `Firebase:ProjectId` | Envío de push (FCM) | Fase 5 (push) |
| service account JSON | — | Credencial del servidor para FCM | Fase 5 (push) — ver abajo |

## Notas
- **Google Cloud Console** → el OAuth client debe tener registrado el redirect URI exacto:
  `https://petproductivity.onrender.com/api/auth/google/callback`
- **Firebase** (Fase 5): el service account JSON no se commitea. En Render se monta como *Secret File* o se pasa su contenido por env var, y se referencia por ruta/contenido al inicializar `FirebaseAdmin`.
- **`google-services.json`** (app Android, Fase 5): va en `src/PetProductivity.Client/Platforms/Android/`, **gitignored**. Cada quien usa el suyo.
- El cliente apunta por defecto a Render (`Constants.BaseUrl`). Para desarrollo local contra el emulador: Ajustes → Dirección del Servidor → `http://10.0.2.2:5051`.

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
  URL `https://petproductivity.onrender.com/health` → intervalo **5 min** → alerta al email.
  Además de avisar caídas, el ping evita/reduce el cold start de Render (~23 s medidos) — sinergia T10-B.
- **CI:** `.github/workflows/ci.yml` corre los tests del server en cada push a `main` (el cliente MAUI
  queda fuera a propósito). Render no espera al CI: el rojo avisa, no bloquea.

## Verificar un deploy
- `GET https://petproductivity.onrender.com/api/shop/catalog` → 200 **y con contenido** (un `[]` = imagen sin `Catalog/`).
- Las migraciones EF se aplican solas al arrancar (`db.Database.Migrate()`).
