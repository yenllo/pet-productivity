# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
#
# --platform=linux/amd64 fijo: los dynos de Heroku son amd64. Sin esto, algunos hosts de build
# resuelven la imagen multi-arch a la arquitectura del propio host de build → binarios ARM64
# corriendo en un dyno amd64, que segfaultean (exit 139) hasta en /bin/sh.
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project file(s) and restore dependencies
COPY src/PetProductivity.Server/PetProductivity.Server.csproj src/PetProductivity.Server/
COPY src/PetProductivity.Shared/PetProductivity.Shared.csproj src/PetProductivity.Shared/
RUN dotnet restore src/PetProductivity.Server/PetProductivity.Server.csproj

# Copy the remaining source code
COPY src/ src/

# Catálogo de la tienda (fuente de verdad en /Catalog, raíz del repo): sin esto la imagen
# se construye con la tienda VACÍA — el csproj copia sus info.json al publish desde ../../Catalog.
COPY Catalog/ Catalog/

# Build and publish the application
WORKDIR /source/src/PetProductivity.Server
RUN dotnet publish -c Release -o /app/publish

# Build the runtime image
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# DEBUG TEMPORAL: diagnóstico completo en un solo ciclo.
ENTRYPOINT ["/bin/sh", "-c", "echo DBG_A_sh_ok; uname -m; dotnet --info; echo DBG_B_launching; ASPNETCORE_HTTP_PORTS=${PORT:-8080} dotnet PetProductivity.Server.dll & PID=$!; sleep 5; kill -0 $PID 2>/dev/null && echo DBG_C_alive_after_5s || echo DBG_C_dead_after_5s; wait $PID; echo DBG_D_exitcode=$?; sleep 25"]
