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

# Escucha en $PORT si el host lo define en runtime (Heroku: puerto dinámico por dyno),
# y cae al 8080 por defecto (Render / local: puerto fijo de la imagen aspnet).
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet PetProductivity.Server.dll"]
