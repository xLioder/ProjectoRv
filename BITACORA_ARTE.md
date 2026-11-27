# Bitácora de arte · Control de Calidad XR

Registro de todo lo realizado en la producción visual (modelos, rig, animaciones, escena).
Rama de trabajo: `art/visual-pass` (creada desde `origin/petro`). Sin push: los commits quedan en local.

---

## Fase 1 · Auditoría (sin cambios)

- Proyecto real en `origin/petro`: Unity **6000.6.3f1**, URP 17.6, **XRI 3.6.1** + OpenXR 1.18 + Meta XR SDK 207.
- 17 scripts de juego en `_Project/Scripts` + 2 builders de editor (placeholders con primitivas).
- Requisitos que imponen los scripts al arte documentados en `ProyectoFinalRV/Assets/_Project/Art/README_Arte.md`.

## Fase 2 · Productos y defectos — commit `3d0255d`

- Generador reproducible `QCXR > Arte > Fase 2` (`Scripts/Editor/Art/QCXRArtBuilder.cs`, `QCXRMeshKit.cs`).
- Estética industrial estilizada: chaflanes facetados, paneles atornillados, franjas de peligro, LEDs.
- Un único material URP con atlas de paleta (albedo + metal/suavidad + emisión) → 1 draw call por producto.
- 4 defectos compartidos (`Prefabs/Defects/`) con `Visual / Fixed / WorkCollider` en la capa `Defect`.
- 5 productos remodelados en sitio (fileID de `Product` intactos → catálogo de `ProductFlow` sigue enlazado).

### Incidencias de entorno (no afectan al repo)
- `com.unity.pipeline 0.10.0-exp.1` no se descargaba por DNS (`download.packages.unity.com`). Se embebió **solo en local**
  (`Packages/com.unity.pipeline`, excluido vía `.git/info/exclude`; `manifest.json` intacto). Recomendación al equipo:
  quitar ese paquete experimental si nadie lo usa.
- `packages-lock.json` cambia por ese embebido → **nunca se incluye en los commits de arte**.
- Error preexistente del Meta XR Audio SDK 85 (`MetaXRAcousticMap.cs` usa `UnityEditor.GUID` en runtime). No lo toqué.

---

## Fase 3 · Herramientas y manos VR — completada

### Qué se hizo
- **Herramientas** (`QCXRToolBuilder.cs`, editadas en sitio, fileIDs de `Tool`/`Tip`/`Attach`/`LensPoint` conservados):
  - Martillo 0.27 m: mango de goma con anillos, cuello de acero, cabeza con banda naranja, cara de golpe cromada (-Y), uña partida.
  - Destornillador 0.24 m: mango hexagonal con franja de peligro, `Shaft` **separado** con pivote en su raíz y eje +Z
    (corregido: el placeholder giraba sobre el eje equivocado), punta Phillips.
  - Lupa 0.29 m: aro naranja remachado, doble bisel cromado, cristal `QCXR_Glass` (único transparente), `LensPoint` +Z → -Y.
  - Colliders de cápsula/caja en hijo `Colliders`; `HandPoseHint` en cada herramienta.
- **Manos** (`QCXRHandBuilder.cs`): guantes izquierdo y derecho, 21 huesos cada uno (muñeca + 5 dedos × 3 + puntas),
  malla con piel rígida, 10 poses, 2 transiciones, 5 acciones aditivas, `Hand_R.controller` (3 capas) + `Hand_L.overrideController`.
- **Scripts visuales** `HandPoseDriver` y `HandPoseHint` (`Scripts/Visual/`). Entradas XRI ya asignadas en los prefabs.
- **Verificación**: renders de las 10 poses y de cada mano sujetando cada herramienta por su `Attach` → el mango pasa por
  el centro del puño en las 6 combinaciones; espejo izquierda/derecha correcto.

### Ajustes tras la revisión visual
- Yemas de los dedos rehechas (caja corta + media esfera) porque el chaflán grande las hacía parecer púas.
- Uña del martillo unida a la cabeza (antes flotaba).
- Encuadre de la hoja de poses calculado con los huesos (las bounds del SkinnedMeshRenderer son fijas).

### Incidencias
- Unity en batch se colgaba al cerrar tras terminar. Otra sesión añadió `RunBatch` (sale con `EditorApplication.Exit`).
  Maté mi proceso p3b colgado (PID 14796 + worker 26420) después de confirmar que su trabajo estaba guardado.

### Pendiente para la Fase 6 (escena)
- Colgar `Hand_L`/`Hand_R` del punto de agarre de cada controlador del XR Origin, ocultar el modelo de mando de Starter
  Assets y asignar `HandPoseDriver.interactors`. Validar la orientación en el visor (solo se toca `HandOffset`).

---

## Fase 3 · Notas de diseño

### Decisiones tomadas (con autonomía delegada)
- **Convención de agarre única**: el mango de cada herramienta va por +Z y `Attach` (rotación identidad) está en el centro
  del puño. La raíz de cada prefab de mano está en ese mismo punto (centro del puño) con +Z = eje del puño, por lo que
  al agarrar, mano y herramienta coinciden sin ajustes por herramienta.
- **Manos = guantes de trabajo industriales** (naranja/grafito/crema, puño con franja de peligro): encajan con la
  estética y simplifican la anatomía manteniendo dedos claramente legibles.
- **Rig por huesos con piel rígida** (cada pieza 100 % a un hueso + nudillos esféricos que tapan las uniones).
  Descartados blend shapes (no sirven para flexión de dedos) y Animation Rigging (dependencia innecesaria).
- **Sin seguimiento de manos obligatorio**: las poses se manejan con grip/gatillo del controlador (válido en Quest 2 y 3).
- **Scripts visuales mínimos fuera de los 17** (`Scripts/Visual/`): `HandPoseDriver` (grip/gatillo + eventos de agarre XRI
  → Animator) y `HandPoseHint` (qué pose usa cada objeto). No tocan GameEvents ni lógica de juego.
- **Acciones de mano solo en dedos** (aditivas): mover la muñeca separaría la mano de la herramienta, que sigue al
  controlador real. Así se respeta el control físico del jugador.

---

## Fase 4 · Estación y entorno — completada

### Decisiones tomadas (con autonomía delegada)
- **Montaje sobre copia**: `QCXR_MainScene` (`Assets/_Project/Scenes/`) se genera copiando `SampleScene` (que no se toca)
  para conservar todos los objetos con scripts y sus referencias ya conectadas. Solo se quitan las mallas primitivas
  (los colliders siguen) y se añaden prefabs visuales. Es reproducible: `QCXR > Arte > Fase 4 · Montar QCXR_MainScene`.
- **XR Origin recolocado** a `(0,0,0)`: en `petro` estaba a 0.87 m de altura y dentro de la banda (z 0.5).
- **Banco de herramientas** movido delante de la banda (z 0.24–0.42, superficie a 0.8 m) con las herramientas tumbadas
  a lo largo de X sobre siluetas. Antes la mesa quedaba entre jugador y banda y el centro de la banda a >0.9 m.
- **Consola** con frente hacia el jugador (−X) y botones uno al lado del otro en Z (INICIAR TURNO más cerca).
  El botón rojo sigue apagándose fuera del atasco (lo hace RandomEventSystem); su carcasa es parte de la consola y
  siempre se ve.
- **Estantes**: cada `Shelf_X` recibe un `AttachPoint` para que el producto quede apoyado en su base y mirando al
  jugador (antes el centro del producto se encajaba a la altura del socket y se hundía en la balda).
- **Banda**: `ConveyorBelt.beltRenderer` apunta a la superficie nueva y `textureScroll = 1/0.48` para que la textura
  avance a la misma velocidad que los productos (función ya existente en el script; no se programó movimiento).
- **Iluminación Quest**: una sola direccional con sombras + ambiente Trilight; luminarias del techo emisivas
  (sin luces extra). Paredes, techo y suelo no proyectan sombra. Objetos de entorno marcados como estáticos
  (batching + GI) para poder hornear luz más adelante sin tocar nada.
- **Paleta ampliada** (Concrete, ConcreteDark, LampWhite, SafetyGreen, WallPanel) añadiendo celdas al final del atlas,
  sin mover las existentes (los modelos de las fases 2 y 3 no cambian).

### Incidencia de entorno: Unity se cerraba al arrancar (resuelta)
- Síntoma: desde las 10:32 cada ejecución batch moría con `Shut down.` (exit 127) durante la recarga de ensamblados,
  antes de ejecutar ningún método. No era la otra sesión de Claude (lo confirmó) ni la red.
- Causa (Visor de eventos de Windows): **fallo nativo en `OVRPlugin.dll` (Meta XR SDK 1.207), excepción 0xc0000409**,
  dentro de `OVRTelemetry startSession`. El estado local de la telemetría de Meta
  (`%LOCALAPPDATA%\Oculus\Unity\Unity.json`, reescrito a las 10:28) quedó inválido.
- Arreglo: renombré ese archivo a `Unity.json.bak-20261117` (no se borró; el SDK lo regenera). Desde entonces Unity
  arranca bien. **No toca el proyecto**: si a otro integrante le pasa lo mismo, hacer lo mismo en su equipo.
- También borré `Library/LastSceneManagerSetup.txt` y un `Temp/UnityLockfile` huérfano (estado regenerable de Unity).

### Ajustes tras la revisión visual
- Etiquetas de los estantes (TMP de `ProductStation`): eran enormes y se solapaban → movidas al canto frontal de cada
  balda, tamaño legible, mirando al jugador. Etiquetas de los botones colocadas sobre cada botón.

### Resultado de la Fase 4
- 10 prefabs de entorno (`Prefabs/Environment/`) y `QCXR_MainScene` montada y verificada con renders desde el punto
  de vista del jugador (frente, izquierda, derecha) y dos vistas generales.
- Todo al alcance desde el origen: banda a 0.5–1.0 m, herramientas a 0.24–0.42 m, consola y cubo a la derecha,
  estantería a la izquierda.
- Pendiente para fases siguientes: animaciones y partículas que usan los anclajes creados aquí (`Rotor`, `SparksAnchor`,
  `BayLight`, `FlashRing`, `Cap/PressedPos`) → Fase 5. Manos en el XR Origin y reorganización de jerarquía → Fase 6.

---

## Fase 5 · Animaciones y VFX — completada (commit 47d25d9)

### Decisiones tomadas (con autonomía delegada)
- **Animación "al activarse"** para todo lo que los scripts ya encienden/apagan: el clip es el estado por defecto
  del Animator, así arranca solo sin código nuevo. Aplica a: tornillo apretándose y chapa enderezándose (en `Fixed`),
  revelar grieta (en `Visual`), producto entrando a la banda (en `Model`), balizas (en `Env_*Lit`), paneles de UI
  y sello de nota.
- **Un único `PresentationBridge`** (`Scripts/Visual/`, fuera de los 17, solo visual) para lo que depende de un evento:
  vibración de la banda (`BeltJamChanged`), polvo del martillo (Sfx `Hit`), destello de reparación (Sfx `Fixed`),
  destello verde/rojo del hueco o del cubo (Sfx `Success`/`Fail`/`Report`, que `ProductStation` ya emite con su
  posición) y sello + confeti (`SessionSummaryReady`).
- **Botones**: `XRButton.onPressed` (UnityEvent existente) → `Animator.SetTrigger("Press")` de la tapa, conectado
  como listener persistente desde el editor (sin código).
- **Sin animaciones propias de herramientas**: el movimiento es físico (lo hace el jugador); las acciones de mano
  (golpe, giro, inspección) ya están en la Fase 3 y el giro del eje sigue siendo de `Tool.cs`. No se duplica nada.
- **Fundido de salida de UI**: el clip `UI_FadeOut` existe (trigger `Hide`), pero `GameUI` apaga los paneles con
  `SetActive(false)` al instante; para verlo, el programador debe llamar al trigger y desactivar al terminar.
- **Partículas Quest**: 3 materiales compartidos (aditivo, alfa, confeti sin textura), texturas de 64 px, máximo
  120 partículas (confeti) y ≤ 24 en el resto; sin sombras ni sondas.

### Resultado de la Fase 5
- 17 clips + 9 controladores (+1 override), 5 prefabs de partículas, 4 sellos de nota, `PresentationBridge`.
- Verificado con renders: efectos simulados a media vida y animaciones de defectos muestreadas (tornillo inclinado →
  plano, chapa aplastada → rebote, grieta que se propaga). En la escena guardada: 6 destinos de destello, 4 sprites
  de nota y los 2 botones conectados a su tapa.
- Ajuste tras revisión: el humo del circuito era casi invisible → más opaco, más grande y más denso (se aplica en la
  siguiente regeneración).
- Incidencia: `GetComponent<T>() ?? AddComponent<T>()` falla en Unity (nulo falso) → cambiado a comprobación explícita.

---

## Fase 6 · Prefabs y escena final — completada

### Decisiones tomadas (con autonomía delegada)
- **Manos**: hijas de `Left/Right Controller` del XR Origin existente (no se sustituye nada); modelo del mando de
  Starter Assets desactivado; el driver escucha Near-Far + Poke (teletransporte excluido para que no cambie la pose).
- **Jerarquía del brief** aplicada moviendo/renombrando objetos (sin recrearlos): GameManager · Environment ·
  Factory · Products · Tools · XR Origin · UI · Effects. Verificado que ningún script de juego busca por nombre.
- **Productos de referencia** en sus estantes, desactivados y EditorOnly (activos, el socket los puntuaría).
- **Build Settings**: `QCXR_MainScene` primera; `SampleScene` se mantiene detrás (único cambio en ProjectSettings
  que se incluye en el commit).
- Un solo menú (`Fase 4-6 · Montar QCXR_MainScene completa`) regenera estación, animaciones, efectos y escena final.

### Verificación
- Log del montaje: manos L y R con 2 interactores cada una (Poke + Near-Far); jerarquía final volcada en el log y
  coincidente con el brief; renders de la escena sin regresiones (sello de nota visible en Results).
- Humo del circuito regenerado con la versión más visible.

---

## Fase 7 · Control de calidad — completada (auditoría final: 157 OK · 0 fallas · 1 aviso intencional)

### Qué se hizo
- **Recursos 2D (sección 7 del brief, que faltaba)**: 5 iconos de producto y 4 de defecto (render de los modelos con
  alfa por doble pasada negro/blanco), panel, marco, botón, franja decorativa e indicadores OK/error/aviso. Aplicados:
  iconos en los botones del reporte, paneles con fondo/marco/franja, iconos de producto en los estantes.
- **Optimización**: sombras a 8 m, formatos ASTC en Android para todas las texturas del arte.
- **Auditoría automática** (`QC_Informe.md`) contra el brief + menú `Reconstruir todo (Fases 2-7)` que regenera todo
  desde cero (probado: reconstrucción completa sin errores).

### Problemas encontrados por la auditoría y corregidos
- **Grieta sin emisión (bug real)**: la validación de URP retiraba la palabra clave `_EMISSION` del material de la grieta
  → el brillo al revelar con la lupa NO se habría visto. Tras dos intentos (flags de GI, color casi negro) se optó por la
  solución robusta: `QCXR_Crack` pasa a **URP Unlit** y el revelado anima `_BaseColor` (oscuro → naranja → rescoldo),
  que en Unlit se ve como brillo propio sin depender de palabras clave (y es más barato en Quest).
- **Reloj demasiado profundo** (8.1 cm vs 6 cm): llave de cuerda aplanada y agujas pegadas a la esfera → 7.0 cm.
- **Poses Press e Inspect** existían como clips pero no eran alcanzables: añadidas al Animator como poses 5 y 6
  (`HandPose.Press`, `HandPose.Inspect`).
- **Humo del circuito en capa Default** dentro del defecto: ahora hereda la capa `Defect`.
- Falso negativo de la auditoría ("URP activo"): el proyecto asigna URP por nivel de calidad; comprobación corregida.

### Incidencias de API (Unity 6000.6)
- `TMP_Text.enableWordWrapping` y `SerializedProperty.objectReferenceInstanceIDValue` son errores de compilación
  (obsoletos) → `textWrappingMode` y `objectReferenceEntityIdValue`.

---

## Verificación posterior · Build de prueba para Quest (rama `Abeth`)

Tras fusionar las PR #1 (→ `petro`) y #2 (→ `Abeth`), se trabaja desde `Abeth`.

- Nuevo menú `QCXR > Arte > Build de prueba Quest (APK)` (`QCXRBuildCheck.cs`, solo editor): compila
  `QCXR_MainScene` para Android con los Player Settings tal como están, sin modificarlos.
- **Resultado: build correcta** · IL2CPP · ARM64 · 29.9 min · **APK de 70.6 MB** (214 MB descomprimido).
  El "1.2 GB" que muestra el informe de Unity incluye símbolos e intermedios que no van en el APK.
  - Contenido propio del juego: 36 MB sin comprimir (texturas 20.7 MB, shaders 8.8 MB, mallas 4.3 MB).
  - Lo más pesado es nativo: `libil2cpp.so` 92 MB (crece con el `com.meta.xr.sdk.all` completo), `libunity` 25 MB,
    `libOVRPlugin` 7 MB, `libmrutilitykitshared` 5 MB.
- **Manifiesto verificado**: incluye `com.oculus.intent.category.VR`, `android.hardware.vr.headtracking` y
  `com.oculus.supportedDevices` = quest2 / quest3 → el APK arranca como app VR.
- **16 NullReferenceException durante la build** (la build termina bien igualmente): todas vienen del editor del
  paquete OpenXR (`FeatureHelpersInternal.RefreshAllFeatureInfo`), que encuentra una "feature" nula al recorrer la
  configuración de OpenXR; arrastra a otros procesadores del manifiesto (Meta, XR Hands, XR Management). No es del
  arte ni de los 17 scripts. Recomendación al equipo: abrir *Project Settings > XR Plug-in Management > OpenXR*,
  revisar features en rojo/faltantes y, si sobra, quitar paquetes Meta que no se usen (Voice, MRUK, Platform).
- El error de `MetaXRAcousticMap.cs` (Meta XR Audio) no bloqueó la build de jugador.

---

## Complemento · Animaciones de respuesta de las herramientas (rama `Abeth`)

- Detectado al repasar el encargo: la sección 8 pedía movimientos propios de martillo (golpe y recuperación),
  destornillador (presión) y lupa (inspección, acercamiento, orientación) y no se habían hecho (había decidido que
  bastaba con el movimiento físico + acciones de mano). Ahora sí:
  - `Hammer_Hit`, `Screwdriver_Press`, `Magnifier_Focus`, `Magnifier_Reveal` + controladores `Tools/Tool_*.controller`.
  - Solo animan piezas hijas; la raíz sigue siendo de la mano real. El giro del eje no se duplica.
  - `MagnifierFocusFX` (visual, solo lectura) y `PresentationBridge` ampliado (Sfx `Hit`, `Screw`, `Reveal`).
  - Pivotes de `Head` y `Frame` centrados; aro `FocusRing` añadido.
- Auditoría: **165 OK · 0 fallas · 1 aviso** (8 comprobaciones nuevas de herramientas).
- Blender: está instalado vía Steam y el conector MCP de Blender Lab está configurado, pero falta el complemento
  dentro de Blender (repositorio de extensiones `https://lab.blender.org/`). Se deja para más adelante por decisión
  del usuario; los modelos siguen generándose desde Unity.
- Revisión visual (hoja `Anim_Tools`): el primer intento del golpe hacía desaparecer la cabeza del martillo porque el
  clip solo animaba la componente Y de la posición (las otras quedaban en 0 y la cabeza saltaba al mango). Corregido
  animando el vector completo (también en el eje del destornillador). Verificado: aplastamiento + rebote en su sitio.

---

## Reaparición de herramientas (petición del usuario, rama `Abeth`)

- Problema: el jugador no se desplaza; si una herramienta cae de la mesa no puede recogerla. `Tool.cs` tenía
  `ReturnHome` con `fallY = -0.5`, que nunca se cumple (suelo en y = 0) y además no comprueba si se está sujetando.
- Solución sin tocar `Tool.cs`: componente `ToolRespawn` en los 3 prefabs (la escena lo recibe sin modificar su
  archivo). Reaparece en su sitio si nadie la sujeta y lleva 0.8 s caída (< 0.55 m) o fuera de alcance (> 0.9 m).
  Efecto de escala + `SfxId.Spawn`.
- Prueba con física real en editor: **10/10 OK** (suelo → vuelve; caída del banco → vuelve y se queda; sobre la
  banda → se queda).
- **Protección añadida**: el equipo ya edita `QCXR_MainScene` a mano; los menús que la regeneran ahora piden
  confirmación para no borrar ese trabajo.
- Sincronización: `Abeth` estaba 13 commits por detrás (merges de petro/Esteban/Elam). Los ajustes que Unity había
  reescrito en local se guardaron en un `git stash` y `.vsconfig`/`.slnx` locales se movieron a una copia de seguridad.

---

## Botones que solo funcionaban al agarrar + referencias rotas en la escena (rama `Abeth`, en curso)

- **Causa de los botones**: el `Poke Interactor` del rig (XRI 3) tiene `Require Poke Filter` activado y los `Btn_*`
  no tenían `XRPokeFilter` → solo el agarre podía seleccionarlos. Arreglo: `XRPokeFilter` con pulsación hacia abajo
  (-Y, 45°), igual que el botón de ejemplo de XRI (`QCXRButtonPokeSetup`).
- **Problema mayor descubierto al guardar la escena**: la escena del equipo tenía **9 referencias rotas** hacia
  componentes internos de prefabs de arte (5 luces de estantes, anillo del cubo, Animator de la banda, 2 tapas de
  botón). Causa: mis generadores RECREAN esos prefabs (nuevos identificadores internos) y el equipo editó la escena a
  partir de una versión anterior. Efecto en juego: sin destellos verde/rojo de estantes y cubo, sin vibración de la
  banda en el atasco y sin animación de las tapas de los botones.
- **Herramienta de reparación** `QCXR > Escena > Reparar referencias del arte + botones con el dedo`
  (`QCXRSceneRelink`): vuelve a enlazar POR NOMBRE sobre la escena tal cual (sin regenerarla), añade el poke a los
  botones, guarda una vez y cuenta referencias rotas antes/después.
- **Prevención**: los generadores que recrean prefabs (manos, estación, efectos) ahora piden confirmación fuera de un
  montaje completo y remiten a la herramienta de reparación.
- **Bloqueo**: el fallo nativo de `OVRPlugin.dll` (Meta XR SDK 1.207, 0xc0000409 en la telemetría) volvió en modo
  batch: 4 intentos seguidos, incluso renombrando de nuevo `%LOCALAPPDATA%\Oculus\Unity\Unity.json`. La reparación
  queda pendiente de ejecutarse desde el editor de Unity normal (un clic de menú).
