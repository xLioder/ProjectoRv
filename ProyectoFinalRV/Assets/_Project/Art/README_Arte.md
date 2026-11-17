# QCXR · Arte — guía de integración

Generado por `QCXR > Arte > Fase 2 · Productos y defectos` (`Scripts/Editor/Art/QCXRArtBuilder.cs`).
El generador es reproducible: volver a ejecutarlo regenera mallas, materiales y prefabs sin romper referencias.

> ⚠️ **No vuelvas a ejecutar `QCXR > Construir layer, config y prefabs`** (el builder antiguo de placeholders):
> sobrescribe `Product_*.prefab` con primitivas. Si ocurre, basta con ejecutar de nuevo el menú de Arte.

## Dirección visual

Industrial-estilizado: carcasas robustas con chaflanes marcados y sombreado facetado, paneles atornillados,
remaches, franjas de peligro y LEDs emisivos. Base crema / grafito / acero con acentos naranja de fábrica, y
un color de identidad por producto (Robot azul, Radio rojo-naranja, Lámpara amarilla, Tostadora cromo, Reloj verde).

## Materiales (rendimiento Quest)

| Material | Uso | Notas |
|---|---|---|
| `Art/Materials/QCXR_Atlas.mat` | **Todos** los productos y defectos | URP Lit. Paleta 64×64 px (8×8 celdas): albedo, metálico/suavidad y emisión. Una malla por producto → 1 draw call. |
| `Art/Materials/QCXR_Crack.mat` | Grieta oculta | URP Unlit; la animación de revelado (Fase 5) anima `_BaseColor` oscuro → naranja → rescoldo (brillo sin palabras clave de emisión). |

La paleta está definida en `QCXRMeshKit.cs` (`enum Sw` + `Palette.Get`). **No reordenes el enum**: las UV apuntan a las celdas.

## Defectos compartidos — `Prefabs/Defects/`

Origen = punto sobre la superficie; **+Y local = normal de la superficie**. Todo en la capa `Defect`.

```
Defect_X                  ProductDefect (definición ya configurada)
├── Visual                defectVisual  → aspecto dañado
├── Fixed  (inactivo)     fixedVisual   → aspecto reparado
└── WorkCollider          SphereCollider trigger → workCollider
```

| Prefab | Herramienta | Trabajo | Reparable | Oculto | Visual | Fixed |
|---|---|---|---|---|---|---|
| `Defect_LooseScrew` | Destornillador | 2 s | ✔ | — | `ScrewLoose`: levantado 1 cm, inclinado 18°, rosca a la vista, marca roja | `ScrewHead`: asentado. **Pivote en el eje del tornillo** (giro + descenso, Fase 5) |
| `Defect_Dent` | Martillo | 3 golpes | ✔ | — | `PlateDented`: placa de blindaje 6×6 cm con hundimiento facetado | `PlateSmooth`: placa lisa (pivote centrado, Fase 5) |
| `Defect_HiddenCrack` | — | — | ✘ | ✔ | `Crack`: grieta fina ramificada (material `QCXR_Crack`) | vacío |
| `Defect_BurnedCircuit` | — | — | ✘ | — | `Board`: escotilla abierta, placa chamuscada, chip derretido, condensador reventado, brasas. Hijo `SmokeAnchor` (+Z hacia fuera) para el humo | vacío |

### Comportamiento heredado de `ProductDefect.Refresh` (importante)

- **Inactivo** → `Visual` y `Fixed` apagados: el producto se ve sano con su malla base.
- **Oculto** (grieta) → `Visual` apagado. La grieta es invisible hasta que la lupa la revela; el brillo de 0.4 s
  debe reproducirse al **activarse** `Visual` (estado por defecto del Animator, Fase 5).
- **Reparado** → `Visual` se apaga y `Fixed` se enciende en el mismo frame: las animaciones de reparación
  (tornillo apretándose, chapa enderezándose) viven en los hijos de `Fixed` y arrancan al activarse.
- `onFixed` (UnityEvent del componente) sirve para disparar el destello de reparación desde el inspector.

## Productos — `Prefabs/Products/`

Frente = **−Z** (mira al jugador sobre la banda), arriba = +Y, pivote en el centro de la envolvente.
Los componentes de raíz (`Rigidbody`, `XRGrabInteractable`, `Product`) se conservan: el catálogo de
`ProductFlow` en la escena sigue apuntando a ellos.

```
Product_X                 Rigidbody · XRGrabInteractable · Product (defects[] = instancias de abajo)
├── Model                 MeshRenderer (QCXR_Atlas) — aquí irá la animación de entrada con rebote (nunca en la raíz)
├── Colliders             BoxCollider(s) físicos para agarre, banda y estantes
└── DefectSlots
    └── Slot_<Tipo>       punto de anclaje (posición + orientación de la superficie)
        └── Defect_<Tipo> instancia del prefab compartido (escala por producto)
```

| Producto | Envolvente aprox. (m) | Slots (cara) | Tris |
|---|---|---|---|
| Robot (tutorial) | 0.18 × 0.22 × 0.12 | Tornillo (cubierta) · Abolladura (pecho) · Grieta (cubierta) | 2 879 |
| Radio | 0.30 × 0.16 × 0.10 | Tornillo (techo) · Grieta (techo) · Circuito (techo) | 1 804 |
| Lámpara | base 0.14 × 0.10 × 0.14 + brazo/pantalla (alto ≈ 0.26) | Tornillo (base) · Abolladura (base) · Circuito (frente) | 1 317 |
| Tostadora | 0.26 × 0.13 × 0.15 | Abolladura (frente) · Grieta (techo) · Circuito (frente) | 629 |
| Reloj | 0.18 × 0.18 × 0.06 | Tornillo (esquina frontal) · Abolladura (techo) · Grieta (esfera) · Circuito (techo) | 1 432 |

Cambios respecto a los placeholders: los slots del **Reloj** ya no se solapan (se repartieron entre frente y techo)
y los de la **Lámpara** quedan fuera de la sombra de la pantalla (brazo y pantalla movidos atrás-izquierda).

## Requisitos de scripts que el arte respeta

- Animación de desaparición (`Product.Vanish`) escala la raíz → no animar la raíz del producto.
- `ConveyorBelt` mueve la raíz con `MovePosition` → la animación de entrada va en `Model`.
- `Tool.FindDefectAtTip` busca colliders en la capa `Defect` → todo `Defect_*` está en esa capa.
- La lupa revela por distancia/ángulo a `defect.transform.position` → el origen del defecto está sobre la superficie.

---

# Fase 3 · Herramientas y manos

Generado por `QCXR > Arte > Fase 3 · Herramientas` (`QCXRToolBuilder.cs`) y `QCXR > Arte > Fase 3 · Manos` (`QCXRHandBuilder.cs`).

## Convención de agarre (común a herramientas y manos)

- El mango de cada herramienta va a lo largo de **+Z local** y `Attach` (rotación identidad) está en el centro del puño.
- La raíz de `Hand_L` / `Hand_R` es ese mismo punto: **origen = centro del puño cerrado, +Z = eje del puño** (sale por el
  lado del índice). Al colgar la mano del punto de agarre del controlador, la herramienta agarrada (alineada por XRI con
  su `Attach`) queda dentro del puño sin ajustes.
- Si en el visor hiciera falta corregir la orientación, se ajusta **solo** `HandOffset` (no afecta a huesos ni clips).

## Herramientas — `Prefabs/Tools/`

Editadas en sitio: se conservan `Tool`, `XRGrabInteractable`, `Rigidbody` y los objetos de referencia con sus fileID.

```
Tool_Hammer        Handle · Head · Tip (cara de golpe, -Y) · Attach · Colliders
Tool_Screwdriver   Handle · Shaft (SEPARADO, pivote en su raíz, gira sobre +Z) · Tip · Attach · Colliders
Tool_Magnifier     Handle · Frame · Lens (QCXR_Glass) · LensPoint (+Z mira a través del cristal) › LensLight · Tip · Attach · Colliders
```

- `Tool.spinPart` → `Shaft` (corrige el placeholder, cuyo eje estaba en Y y no giraba sobre sí mismo).
- `Tool.lens` → `LensPoint` (rotación 90° en X: su +Z apunta a -Y, hacia lo que hay bajo la lupa).
- Cada herramienta lleva `HandPoseHint` con su pose (Hammer / Screwdriver / Magnifier).
- `QCXR_Glass.mat`: URP Lit transparente, sin sombras ni profundidad (el único transparente del set).

## Manos — `Prefabs/Hands/Hand_L`, `Hand_R`

Guantes de trabajo industriales. Malla con piel rígida (`SkinnedMeshRenderer`, 1 hueso por vértice), material atlas.

```
Hand_R                      Animator (Hand_R.controller) · HandPoseDriver
├── HandOffset              conversión marco de modelado → marco de agarre (ajustable)
│   ├── Wrist
│   │   ├── Thumb1 › Thumb2 › Thumb3 › Thumb_Tip
│   │   ├── Index1 › Index2 › Index3 › Index_Tip      (Index_Tip = punto de pulsado/poke)
│   │   ├── Middle1… · Ring1… · Pinky1…
│   └── HandMesh            SkinnedMeshRenderer (rootBone = Wrist)
└── GripPoint               = centro del puño = donde queda el Attach de la herramienta
```

Flexión de dedos = rotación +X local de cada hueso. La mano izquierda es el espejo exacto (X → -X) y usa
`Hand_L.overrideController` sobre el mismo controlador con sus clips espejados.

### Animator (`Animations/Hands/Hand_R.controller`)

| Capa | Modo | Contenido |
|---|---|---|
| Base | Override | Blend 2D `Grip` × `Trigger`: Open (0,0) · Point (1,0) · IndexCurl (0,1) · Fist (1,1) |
| Hold | Override, peso por script | `Pose` (int): 1 Grab · 2 HoldHammer · 3 HoldScrewdriver · 4 HoldMagnifier |
| Action | Aditiva | `Strike` (trigger) · `Press` (trigger) · `Twist` (bool, bucle) · `Inspect` (bool, bucle) |

Clips (`Animations/Hands/Right|Left/`): poses `Open, Fist, Grab, HoldHammer, HoldScrewdriver, HoldMagnifier, Point, Press,
Inspect, IndexCurl`; transiciones `Close` / `Release` (0.2 s); acciones aditivas `Strike` (0.3 s), `PressTap` (0.15 s),
`Twist` (bucle 0.5 s), `InspectLoop` (bucle 2 s), `ActionIdle`.

Las acciones mueven **solo los dedos**: la muñeca la da el controlador real (mover la muñeca separaría la mano de la
herramienta). La rotación del eje del destornillador sigue siendo de `Tool.cs`; la mano solo acompaña con el pulgar.

### Scripts visuales (fuera de los 17) — `Scripts/Visual/`

- `HandPoseDriver`: lee `Select Value` / `Activate Value` (XRI Starter Assets, ya asignadas) y los eventos
  `selectEntered/selectExited` de los interactores de su mano → parámetros del Animator. Agarrar un `XRSimpleInteractable`
  (botón) dispara `Press`. **Pendiente de escena (Fase 6):** asignar `interactors` (Near-Far / Poke de cada controlador).
- `HandPoseHint`: componente de datos que indica la pose de agarre de un objeto. Sin él → pose genérica `Grab`.

---

# Fase 4 · Estación y entorno

Generado por `QCXR > Arte > Fase 4 · Montar QCXR_MainScene` (`QCXRStationBuilder.cs` + `QCXRSceneAssembler.cs`).

## Escena `Assets/_Project/Scenes/QCXR_MainScene.unity`

Se crea copiando `Assets/Scenes/SampleScene.unity` (que **no se modifica**) y sustituyendo solo la parte visual.
Todos los objetos con scripts y sus referencias se conservan; a los placeholders se les quita la malla y conservan
su collider. Volver a ejecutar el menú regenera la escena desde cero.

Distribución (jugador en el origen mirando a +Z, todo a ≤ 0.75 m):

| Elemento | Posición | Notas |
|---|---|---|
| XR Origin | (0, 0, 0) | En `petro` estaba a 0.87 m de altura y dentro de la banda |
| Banda | centro (0, 0.9, 0.75), avance −X | `ConveyorBelt.beltRenderer` → `Env_ConveyorBelt/BeltSurface`, `textureScroll = 1/0.48` |
| Banco de herramientas | z 0.24–0.42, superficie 0.8 m | Collider `ToolTable` reajustado; herramientas tumbadas sobre sus siluetas |
| Consola | (0.8, 0.9, 0.25), frente −X | INICIAR TURNO z 0.15 · REINICIAR BANDA z 0.35 (oculto fuera del atasco) |
| Estantería | (−0.85, 0, −0.2), frente +X | Fila baja 0.9 m (Robot, Radio, Lámpara) · fila alta 1.32 m (Tostadora, Reloj) |
| Cubo de desecho | (0.8, 0, −0.3) | Interior oscuro visible, borde a 0.98 m |
| Indicador de atasco | (0, 1.61, 1.08) | Sobre el pórtico de inspección; hijo `SparksAnchor` junto al riel trasero |
| Baliza de emergencia | (0, 2.2, 0.75) | Cuelga del techo; `LitDome` + `Rotor` bajo `EmergencyLight` |

## Prefabs — `Prefabs/Environment/`

| Prefab | Contenido / referencias |
|---|---|
| `Env_ConveyorBelt` | Bastidor, rieles, rodillos, patas, máquina de entrada con cortina de tiras, tolva de salida, pórtico de inspección. `BeltSurface` (material `QCXR_Belt`, UV: V a lo largo, avance −X) · `SurfaceRef` (+Z = sentido de avance) · `InspectionZone` · `SpawnMouth` · `ExitMouth` |
| `Env_ToolBench` | Banco con alfombrilla y siluetas · `Spot_Hammer`, `Spot_Screwdriver`, `Spot_Magnifier` |
| `Env_ButtonConsole` | Pedestal + `Base_StartShift`, `Base_ResetBelt` (carcasas fijas) |
| `Env_ButtonCap_Green` / `_Red` | Tapa móvil (`Cap`, pivote en la base) + `RestPos` / `PressedPos` (−12 mm). Se instancian como hijo `Cap` de cada `Btn_*` |
| `Env_ShelfRack` | Armario + balda alta · `Bay_<Producto>` › `BayLight` (emisivo, destello verde/rojo en Fase 5) + `PlacementZone` |
| `Env_DiscardBin` | Pedestal, cubeta roja con franjas · `FlashRing` (emisivo, destello en Fase 5) · `EntryZone` |
| `Env_JamIndicatorLit` | `LitDome` ámbar + `Rotor` (gira en Fase 5) + `SparksAnchor` |
| `Env_EmergencyBeaconLit` | `LitDome` rojo + `Rotor` |
| `Env_Room` | Suelo con juntas y zona de trabajo señalizada, paredes, columnas, techo con vigas y luminarias emisivas, tuberías, palés, cajas, bidones, armario eléctrico, extintor y carteles |

Cada `Shelf_<Producto>` de la escena tiene un hijo `AttachPoint` asignado como `attachTransform` de su
`XRSocketInteractor`: el producto queda apoyado en su base y con su frente hacia el jugador.

## Iluminación y rendimiento

- Una única luz direccional con sombras suaves + ambiente Trilight. Las luminarias del techo son emisivas (sin luces extra).
- `RandomEventSystem.lights` sigue apuntando a la direccional (el parpadeo de la falla eléctrica funciona igual).
- Paredes, techo y suelo no proyectan sombra. El entorno está marcado como estático (batching + GI), así que se puede
  hornear luz más adelante sin tocar nada.
- Materiales de toda la escena: `QCXR_Atlas` (casi todo), `QCXR_Belt`, `QCXR_Glass`, `QCXR_Crack` y los de UI/TMP.

---

# Fase 5 · Animaciones y VFX

Generado junto con la escena por `QCXR > Arte > Fase 4-5 · Montar QCXR_MainScene` (`QCXRFxBuilder.cs`).

## Clips (`Animations/`) y dónde viven

| Animación | Duración | Controlador | Objeto | Cómo se dispara |
|---|---|---|---|---|
| Tornillo apretándose (`Defect_ScrewTighten`) | 0.4 s | `Defect_Screw` | `Defect_LooseScrew/Fixed` | Al repararse (`Fixed` se activa) |
| Abolladura enderezándose (`Defect_DentStraighten`) | 0.3 s | `Defect_Dent` | `Defect_Dent/Fixed` | Al repararse |
| Revelar grieta (`Defect_CrackReveal`, anima `_BaseColor` del material Unlit) | 0.4 s | `Defect_Crack` | `Defect_HiddenCrack/Visual` | Al revelarla la lupa (`Visual` se activa) |
| Producto entrando (`Product_Enter`) | 0.3 s | `Product_Model` | `Product_*/Model` | Al instanciarse |
| Botón presionándose (`Button_Press`) | 0.15 s | `Button` | `Btn_*/Cap` | `XRButton.onPressed` → `SetTrigger("Press")` |
| Banda atascada (`Belt_Jam`) | bucle | `Belt` | `Env_ConveyorBelt` (mueve `BeltSurface`) | `PresentationBridge` ← `BeltJamChanged` |
| Baliza (`Beacon_Spin`) | bucle | `Beacon` | `Env_JamIndicatorLit`, `Env_EmergencyBeaconLit` | Al activarse (atasco / falla eléctrica) |
| Estante aceptando (`Flash_Good` / `Flash_Bad`) | 0.4 s | `Flash` | `Bay_*/BayLight` | `PresentationBridge` ← Sfx Success / Fail |
| Cubo recibiendo (`Bin_Flash`) | 0.3 s | `BinFlash` (override) | `Env_DiscardBin/FlashRing` | `PresentationBridge` ← Sfx Success / Fail / Report |
| Panel de UI (`UI_FadeIn` / `UI_FadeOut`) | 0.2 s | `UI_Panel` | `HUD`, `Instructions`, `Report`, `Results` | Entrada al activarse; salida con trigger `Hide` |
| Sello de nota (`Stamp_Drop`) | 0.5 s | `Stamp` | `Results/Stamp` | Al mostrarse Results |

No se recrean (ya las hace el código): encogimiento del producto al desaparecer, giro del eje del destornillador,
parpadeo de luces y mensajes de pantalla. Las herramientas no tienen clips propios: su movimiento es físico; las
acciones de mano (golpe, giro, inspección) están en la Fase 3.

## Efectos (`Prefabs/Effects/`, materiales en `VFX/`)

| Prefab | Tipo | Dónde está |
|---|---|---|
| `FX_Sparks` | bucle | `Env_JamIndicatorLit/SparksAnchor` (se enciende con el atasco) |
| `FX_RepairFlash` | una vez | `Effects/` en la escena; el puente lo coloca en el defecto reparado |
| `FX_HammerDust` | una vez | `Effects/`; el puente lo coloca en el punto del golpe |
| `FX_BurnedCircuitSmoke` | bucle | `Defect_BurnedCircuit/Visual/SmokeAnchor` (visible solo con el defecto activo) |
| `FX_Confetti` | una vez | `Effects/`, bajo el panel de resultados |

Materiales: `QCXR_FX_Additive`, `QCXR_FX_Alpha`, `QCXR_FX_Confetti` (URP Particles/Unlit, compartidos).

## Sellos S / A / B / C

`Art/UI/QCXR_Stamp_S|A|B|C.png`: S estrella dorada, A festoneado verde, B hexágono azul, C círculo rojo. El texto
`Grade` (que sigue escribiendo `GameUI`) es hijo de `Results/Stamp`; `PresentationBridge` cambia el sprite según
`SessionRecord.grade` al recibir `SessionSummaryReady` y lanza el confeti.

## `PresentationBridge` (`Scripts/Visual/`, fuera de los 17)

Escucha `BeltJamChanged`, `SfxRequested` y `SessionSummaryReady`. Solo dispara animaciones y partículas; no emite
eventos ni cambia el estado del juego. Está en `Managers/Presentation` con todas sus referencias ya asignadas.

---

# Fase 6 · Prefabs y escena final

Un solo menú regenera todo el montaje: **`QCXR > Arte > Fase 4-6 · Montar QCXR_MainScene completa`**
(`QCXRSceneAssembler` → `QCXRStationBuilder` → `QCXRFxBuilder` → `QCXRSceneFinalizer`). Si se regeneran los
productos (Fase 2) o las manos/herramientas (Fase 3), basta con volver a ejecutar este menú después.

## Jerarquía de `QCXR_MainScene`

```
GameManager                 GameManager + Systems, RandomEvents, ProductFlow, Onboarding, Presentation
Environment                 Floor (collider) · Env_Room (Floor, Walls, Ceiling, Decor, Signage) · Lighting (Directional Light, Global Volume)
Factory
├── ProductSpawnPoint · InspectionPoint · BeltEnd
├── ConveyorBelt            BeltZone (ConveyorBelt) · *_Collider · Env_ConveyorBelt
├── ToolTable               ToolTable_Collider · Env_ToolBench
├── ButtonConsole           Console_Collider · Env_ButtonConsole · Btn_StartShift · Btn_ResetBelt (apagado hasta el atasco)
├── Shelves                 Rack*_Collider · Env_ShelfRack · Shelf_<Producto> (socket + AttachPoint + etiqueta)
├── DiscardBin              DiscardBin_Trigger (ProductStation) · Bin*_Collider · Env_DiscardBin
└── EmergencyIndicator      JamIndicator (atasco) · EmergencyLight (falla eléctrica)
Products                    _LEEME · ReferenceOnShelves (EditorOnly, desactivado: 5 productos en su estante)
Tools                       Tool_Hammer · Tool_Screwdriver · Tool_Magnifier (sobre sus siluetas del banco)
XR Origin (XR Rig)          el de Starter Assets, sin sustituir · Hand_L / Hand_R bajo Left/Right Controller
UI                          GameUI › HUD · Feedback · Instructions · Report · Results (Stamp › Grade)
Effects                     FX_RepairFlash · FX_HammerDust · FX_Confetti
EventSystem
```

Los objetos solo se movieron o renombraron (`*_Collider`, `ProductSpawnPoint`, `DiscardBin_Trigger`); ningún
script del juego busca objetos por nombre, y las referencias serializadas siguen intactas.

## Manos en el XR Origin

- `Hand_L` / `Hand_R` son hijos de `Left Controller` / `Right Controller` (pose de agarre del mando), en el origen.
- El modelo del mando de Starter Assets (`XR Controller Left/Right`) queda desactivado.
- `HandPoseDriver.interactors` = Near-Far + Poke de cada mano (el de teletransporte se excluye a propósito).
- **Validar en el visor**: si la mano no cae justo sobre el mando, ajustar solo `HandOffset` (ver Fase 3).

## Build Settings

`QCXR_MainScene` es la escena 0. `SampleScene` sigue en la lista, detrás (no se modificó).

## Productos de referencia

`Products/ReferenceOnShelves` contiene los 5 prefabs colocados en el `AttachPoint` de su estante para revisar
encajes en el editor. Está **desactivado** y con tag **EditorOnly** (no entra en la build): si se activara,
el socket del estante los "colocaría" y puntuarían.

---

# Fase 7 · Control de calidad, UI 2D y optimización

**Menú maestro: `QCXR > Arte > Reconstruir todo (Fases 2-7)`** — regenera productos, defectos, herramientas, manos,
estación, animaciones, efectos, recursos 2D y la escena, aplica la optimización y escribe el informe de calidad.
`QCXR > Arte > Fase 7 · Auditoría de calidad` solo vuelve a auditar.

## Informe automático — `Art/QC_Informe.md`

Comprueba, contra cada sección del brief: componentes y jerarquía de productos y defectos (capa `Defect`, piezas
independientes, `Product.defects` enlazado, dimensiones ±15 %, pivote), herramientas (`Tip`, `Attach`, `Shaft`
separado y girando sobre su eje, `LensPoint`), manos (21 huesos, 6 articulaciones, 9 poses en el Animator), escena
(grupos del brief, XR Origin conservado, manos con interactores, **referencias rotas**, sockets con `AttachPoint`,
catálogo de `ProductFlow`), rendimiento (triángulos, renderers, materiales, luces, partículas, MSAA, sombras, HDR,
IL2CPP, ARM64, formatos ASTC), recursos 2D, clips y efectos.

## Recursos 2D — `Art/UI/`

| Recurso | Archivos | Uso en la escena |
|---|---|---|
| Iconos de producto | `Icons/Icon_Product_<Robot/Radio/Lamp/Toaster/Clock>.png` (render del modelo real, fondo transparente) | Junto a la etiqueta de cada hueco de la estantería |
| Iconos de defecto | `Icons/Icon_Defect_<LooseScrew/Dent/HiddenCrack/BurnedCircuit>.png` | En los 4 botones del panel de reporte |
| Fondo de panel | `QCXR_UI_Panel.png` (9-slice, se tiñe con `Image.color`) | `Bg` de HUD, Instructions, Report, Results |
| Marco | `QCXR_UI_Frame.png` (9-slice) | `Frame` naranja de cada panel |
| Decoración | `QCXR_UI_Hazard.png` (repetible) | Franja `Deco` superior de cada panel |
| Botón | `QCXR_UI_Button.png` (9-slice con bisel) | Botones de Report y Results |
| Indicadores | `QCXR_UI_Indicator_OK / _Error / _Warning.png` | Disponibles para la lógica de UI |
| Sellos | `QCXR_Stamp_S / A / B / C.png` | `Results/Stamp` (Fase 5) |

Todos los sprites son ASTC 4×4 en Android. No se tocó la lógica ni la navegación de los paneles.

## Optimización Quest aplicada

- `Mobile_RPAsset`: distancia de sombras 50 → **8 m** (la nave mide 5.4 × 4.4 m), 1 cascada. MSAA 4x, HDR apagado,
  IL2CPP y ARM64 ya estaban configurados.
- Texturas del arte en Android: **ASTC 4×4** (sprites/UI), **ASTC 6×6** (banda y partículas); paletas de 64 px sin comprimir
  (color plano, 16 KB).
- Un material atlas para casi todo, entorno estático (batching + GI), una sola luz direccional con sombras,
  partículas ≤ 304 simultáneas en total.
- **Luz horneada: no aplicada a propósito.** `RandomEventSystem` baja la intensidad de las luces en la falla eléctrica;
  con luz 100 % horneada el efecto no se vería. El entorno ya es estático para hornear GI indirecta si se decide.
