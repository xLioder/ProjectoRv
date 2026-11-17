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
| `Art/Materials/QCXR_Crack.mat` | Grieta oculta | URP Lit con `_EMISSION` activo y `_EmissionColor` negro: la animación de revelado (Fase 5) sube la emisión. |

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
