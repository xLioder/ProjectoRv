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
