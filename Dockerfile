# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# DEBUG TEMPORAL: aislar si el contenedor muere por infraestructura o por dotnet.
ENTRYPOINT ["/bin/sh", "-c", "echo BOOT_TEST_MARKER; sleep 60; echo BOOT_TEST_STILL_ALIVE"]
