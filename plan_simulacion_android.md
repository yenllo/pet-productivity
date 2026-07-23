# Plan de Simulación Android y Flujo de Desarrollo

Este documento define la estrategia para correr el cliente .NET MAUI en el emulador de Android y cómo iterar eficientemente en el código.

## 1. Verificación de Entorno
- Se ha verificado que la carga de trabajo `maui-android` está instalada.
- Se ha verificado que existe el AVD `medium_phone` en el Android SDK.

## 2. Arranque y Despliegue Inicial
- Iniciaremos el emulador en segundo plano usando los flags óptimos (deshabilitando carga de snapshots y usando renderizado de GPU indireto para evitar crashes):
  `& "C:\Users\renzo\AppData\Local\Android\Sdk\emulator\emulator.exe" -avd medium_phone -no-snapshot-load -gpu swiftshader_indirect`
- Una vez encendido, desplegaremos la aplicación con el comando oficial de MAUI:
  `dotnet build src/PetProductivity.Client/PetProductivity.Client.csproj -f net8.0-android -t:Run -p:AndroidSdkDirectory="C:\Users\renzo\AppData\Local\Android\Sdk"`

## 3. Configuración del Servidor Objetivo
El cliente `.NET MAUI` necesita saber a qué servidor conectarse.
- **Producción (Por defecto)**: `https://pet-productivity-c03ac5654dd2.herokuapp.com` (Heroku, desde 2026-07-22/23). Esto está codificado en `PetProductivity.Shared/Constants.cs`.
- **Localhost (Desarrollo local)**: Si necesitas probar el servidor local de ASP.NET Core, deberás cambiar la constante o usar el menú de ajustes internos de la app para apuntar a `http://10.0.2.2:5051` (donde `10.0.2.2` es el alias que usa el emulador Android para referirse a tu propia computadora `localhost`).

## 4. El Flujo de Envío de Updates (El Ciclo de Iteración)
Para ver los cambios reflejados rápidamente en el emulador:

### a) Hot Reload (Solo para UI y lógica interna simple)
Para usar Hot Reload (recarga en caliente sin reinstalar), se debe correr la app usando:
`dotnet watch run --project src/PetProductivity.Client/PetProductivity.Client.csproj -f net8.0-android`
*Nota:* `dotnet watch` aplica cambios en archivos `.xaml` y `.cs` al vuelo. Requiere que la app ya esté abierta.

### b) Rebuild + Redeploy (Para cambios estructurales)
Si modificas Modelos (Base de datos, EF Core), inyectas nuevas dependencias (DI) o cambias el archivo `MauiProgram.cs`, el Hot Reload fallará. En ese caso se debe matar el proceso y redesplegar:
`dotnet build src/PetProductivity.Client/PetProductivity.Client.csproj -f net8.0-android -t:Run`

## 5. Observabilidad
Si la aplicación "peta" (crash) o si necesitas leer los logs:
- **Logs en Vivo**: `C:\Users\renzo\AppData\Local\Android\Sdk\platform-tools\adb.exe logcat -s ANTIGRAVITY ANTIGRAVITY_CRASH`
- **Archivo de Crash Local**: La app tiene un manejador global de excepciones en `MainActivity.cs` que escupe los detalles a un archivo. Se puede extraer con:
  `adb shell run-as com.companyname.petproductivity.client cat files/crash.txt > crash_local.txt`
