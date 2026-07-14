# PLAN — Diorama (cuarto) + Tienda de objetos + Monetización
> Documento de **handoff autocontenido**. Un chat nuevo debe poder ejecutar desde aquí.
> Specs detalladas que acompañan: `docs/ROOM_PIECE_SPEC.md` y `docs/SHOP_OBJECTS_SPEC.md` (LEERLAS).
> Fecha: 2026-06-27. Rama: `main` (todo SIN commitear). App: .NET 10 MAUI (ver `CLAUDE.md`/`ROADMAP.md`).
>
> **⚠️ ACTUALIZACIÓN 2026-07-02 (T28):** lo descrito abajo como "sin commit" ya está **commiteado (T9) y desplegado a Render** (migraciones `AddRoomStyle`/`AddPlacedFurniture` aplicadas). F4 y F5.1–F5.3 hechas y **verificadas en emulador**; F5.4 quedó con stub dev (falta Google Play Billing real → T14). El catálogo evolucionó a **carpeta `Catalog/` en disco como fuente única** (189 objetos). La sección "1. Estado actual" de abajo es histórica; el estado vivo está en `CLAUDE.md` §Post-Fase 5. Pendiente: revisión visual del dueño.

## 0. Objetivo
El cuarto isométrico donde vive la mascota debe verse un **cubo cuadrado limpio** (no triángulo), con la
**mascota quieta** en el centro y **muebles** alrededor. Luego: convertir la tienda en una **tienda real**
con cientos de objetos (sprites con varias vistas), colocables tipo **Sims**, y **monetización** (objetos
premium por dinero real en eventos/colaboraciones; comunes con oro).

## 1. Estado actual (working tree, sin commit)
Hecho en sesiones previas (NO commiteado):
- **F1–F3** del diorama ya implementadas: control `Controls/RoomDiorama.cs` (SKCanvasView, 1 timer, evento
  `FrameTick`, props `IsCrystallized`/`RoomStyle`/`Furniture`/`RoomWidth`/`RoomDepth`); estilos+muebles en
  tienda con oro; `Shared/Models/ShopItem.cs` (+`StyleKey`), `Shared/Models/User.cs` (+`ActiveRoomStyle`),
  migración `Server/Migrations/*_AddRoomStyle.cs` (**aplicada NO; se aplica al correr/desplegar server**).
- **Enfoque de cuarto CAMBIADO** a *fondo único intercambiable + muebles por grilla* (se ABANDONÓ el tileado
  modular del atlas; era inviable):
  - `Resources/Raw/room_bg.png` — **pieza inicial ya generada** (cubo iso limpio 1024², ver spec). Reemplazable.
  - `Controls/RoomSprites.cs` — **editado**: carga `room_bg` + muebles (`room_bed/plant/lamp/cat/window`);
    `Ready` = cuando `room_bg` cargó.
  - `Controls/RoomDiorama.cs` — `PaintModularRoom` **reescrito** a: dibuja `room_bg` (fit-contain centrado) +
    muebles desde `RoomGrid` (back-to-front, anclados borde-inferior-centro) + día/noche + motas + frost.
    Constantes del lienzo: `DESIGN=1024, T_W=150, T_H=75, O_X=512, O_Y=330` (coinciden con la pieza).
  - `Controls/RoomGrid.cs` — modelo de grilla NxM + `FurnitureDef`/`FurniturePlacement` (colisiones). Se usa.
  - **`Controls/TileAtlas.cs` + `Resources/Raw/tile_floor_atlas.png` + `tile_wall_atlas.png`** → del enfoque
    abandonado, **ya NO se usan** → BORRARLOS en la limpieza.
- Specs creadas: `docs/ROOM_PIECE_SPEC.md`, `docs/SHOP_OBJECTS_SPEC.md`.
- ⚠️ **SIN VERIFICAR**: el `PaintModularRoom` reescrito aún NO se compiló ni se vio en emulador. **Primer paso
  del chat nuevo = compilar + screenshot.**
- Assets fuente en `IsometricThings/` (untracked, NO debe ir al repo: trae demo Unity con .exe/.dll).

## 2. Decisiones del usuario (firmes)
- **Cuarto = una imagen de fondo** (`room_bg.png`, specs en ROOM_PIECE_SPEC.md). El usuario hará la suya;
  la lógica debe tratarla como intercambiable (soltar otra imagen 1024² con las specs = cuarto nuevo, sin código).
- **Muebles = pack Bongseng** (`IsometricThings/seleccionados/Semi realist room generator sprites appartment/.../Sprites`).
  Solo Bongseng por ahora (coherencia). thurraya (4 vistas, pixel) DIFERIDO.
- **Vistas mixtas** (NO se rehacen): Bongseng = 2 vistas (izq/der) en la mayoría, 4 en `chairs`/`Table`,
  simétricos en `plant`/`carpet`. → **reglas de colocación:**
  - Simétrico (1 vista): piso libre, sin rotación.
  - **2 vistas (izq/der): SOLO pegado a una de las 2 paredes traseras**; la vista se elige por la pared.
  - 4 vistas: piso libre, rotable.
  - **Selección de vista = MIXTA:** automática al colocar + permitir rotar manual entre vistas existentes.
- **Modo edición (arrastrar/mover) = DESPUÉS.** Ahora: colocación en slots fijos que respetan las reglas.
- **Grupos a incluir ahora:** Muebles grandes + Decoración + Vida/animados. (Estructural = futuro.)
- Monetización: premium = 100% cosmético; el oro nunca compra premium. Billing real (Google Play) = última fase.

## 3. FASE 4 — terminar el cuarto (pasos)
1. **Compilar + verificar lo ya reescrito** (antes de nada): build Android + screenshot del Dashboard.
   Esperado: cubo limpio, `room_bed/plant/lamp/cat` en su sitio, mascota centrada, día/noche, ~25 fps.
   Ajustar posiciones/escala de muebles en `EnsureGrid` (RoomDiorama) y los `mod` de tamaño por screenshot.
2. **Curar y organizar assets Bongseng** (el usuario pidió que el agente lo haga y le pase la ruta).
   Copiar a `src/PetProductivity.Client/Resources/Raw/` con convención `obj_<id>_<vista>.png`
   (vistas: `l`,`r`,`tl`,`tr`; simétricos sin sufijo). Set:
   - Grandes (2 vistas→pared): bed, bedside_table, closet, shelf, mirror, tv, lamp, cat_tower.
   - 4 vistas (libres): chair, table.
   - Decoración (simétrico/variantes): plant, carpet, cushion, books, box, teddybear, coffee_cup, wall_painting.
   - Vida/animados: cat (poses), roomba, laptop, window, sliding_door.
   Elegir UNA variante de color por objeto. Atribución en `Resources/Raw/CREDITS.txt` (Bongseng — uso
   comercial+edición OK, no redistribuir). Tip: contar vistas con sufijos `left/right/top left/top right`.
3. **Modelo de vistas/colocación:** extender `FurnitureDef` (en `Controls/RoomGrid.cs`) con
   `ViewSet { Symmetric, LeftRight, FourWay }` y `Slot { Floor, Wall }`. En `RoomDiorama`:
   - elegir el sprite de vista según slot/pared (auto) — guardar las claves `obj_<id>_<vista>` en `RoomSprites`.
   - `EnsureGrid` siembra un set inicial respetando reglas (2-vistas pegados a las 2 paredes traseras; el cuarto
     tiene frente abierto = solo esas 2 paredes; simétricos al piso; 4-vistas al piso).
4. **Limpieza:** borrar `Controls/TileAtlas.cs`, `Resources/Raw/tile_floor_atlas.png`, `tile_wall_atlas.png`
   (enfoque abandonado). Añadir `IsometricThings/` a `.gitignore`.
5. **Verificación:** compila (windows+android); Dashboard cubo limpio con muebles de vista coherente; grupo
   (`PetDetailPage`) usa el mismo `RoomDiorama`. `room_bg.png` reemplazable.

## 4. FASE 5 — Tienda real + objetos + monetización (ver SHOP_OBJECTS_SPEC.md)
- **5.1 Datos+UX:** modelo `RoomObject` (Category/Slot/Footprint/Views/Currency/Rarity/Availability/Source);
  catálogo seed con los objetos curados (con sus vistas); cablear filtros de categoría (hoy estáticos en
  `Views/ShopPage.xaml`); tarjetas con **sprite real** (no emoji); comprar con **oro** + equipar/colocar.
  Tocar `Shared/Models/ShopItem.cs`, `Server/Controllers/ShopController.cs`, `ViewModels/ShopViewModel.cs`,
  `Views/ShopPage.xaml`, `Services/GameDataService.cs`. Migración EF aditiva.
- **5.2 Colocación Sims:** modo edición (arrastrar/rotar/quitar) persistido en `User.PlacedFurniture`
  (slot/celda → objectId + vista). `RoomGrid.IsAreaFree` ya valida colisiones.
- **5.3 Eventos:** `Availability` (fechas) + sección "Limitado" con contador.
- **5.4 Dinero real:** Google Play Billing + validación de recibo server-side para Unique/Event/Special/Collab.
  Requiere Play Console del dueño; NO meter claves en el repo.

## 5. Comandos (ver CLAUDE.md)
- Compilar cliente (rápido, gate): `dotnet build src/PetProductivity.Client/PetProductivity.Client.csproj -f net10.0-windows10.0.19041.0 -clp:ErrorsOnly -nologo`
- Compilar server: `dotnet build src/PetProductivity.Server/PetProductivity.Server.csproj -clp:ErrorsOnly -nologo`
- Emulador (GPU-safe): `& "C:\Users\renzo\AppData\Local\Android\Sdk\emulator\emulator.exe" -avd medium_phone -no-snapshot-load -gpu swiftshader_indirect`
- Desplegar+correr: `dotnet build src/PetProductivity.Client/PetProductivity.Client.csproj -f net10.0-android -t:Run -clp:ErrorsOnly -nologo`
- Screenshot: `adb` (ruta `C:\Users\renzo\AppData\Local\Android\Sdk\platform-tools\adb.exe`): `shell screencap -p /sdcard/s.png` + `pull`.

## 6. Eficiencia de tokens (IMPORTANTE)
- **NO calibrar con builds de Android a ciegas** (cada build ~3 min). Para ajustar geometría/posiciones,
  **simular el ensamblado en PowerShell + System.Drawing** (instantáneo) y solo compilar a Android cuando
  ya se ve bien. Generar previews en el scratchpad y revisarlos.
- Si `SendUserFile` no está disponible, describir/medir por texto.
