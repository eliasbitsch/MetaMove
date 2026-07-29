# Paper Demo (Branch `paper-demo`)

Rein visuelle Show-Demo auf der Quest 3: Passthrough, der GoFa steht im Raum,
und ein gebogener Ray vom Pinch zum Endeffektor zeigt, was die IK gerade
anfahren soll.

**Kein** QR-Marker, **keine** Greifkugel, **kein** ROS/EGM — nichts davon läuft
in dieser Szene. Alles passiert lokal in Unity.

## Bedienung

| Geste | Wirkung |
|---|---|
| Einhändiger Pinch (irgendwo im Raum) | Gebogener Ray erscheint zum TCP, der Endeffektor folgt der Hand (CCD-IK) |
| Pinch lösen | Arm bleibt stehen, Ray blendet aus |
| Zweihändiger Pinch am Roboterkörper | Roboter verschieben / skalieren |

Der Roboter wird ~0,75 s nach dem Start automatisch 1,3 m vor dem Nutzer auf
Bodenhöhe platziert (`DemoRobotPlacer`). Steht er ungünstig: mit zwei Händen
greifen und verschieben.

## Aufbau

| Datei | Rolle |
|---|---|
| `Assets/MetaMove/Scenes/PaperDemo.unity` | Die Szene (wird generiert, nicht von Hand editieren) |
| `Assets/MetaMove/Editor/PaperDemoSceneSetup.cs` | Baut die Szene neu: Menü *MetaMove → Setup Paper Demo Scene (overwrite)* |
| `Assets/MetaMove/Editor/PaperDemoBuild.cs` | APK-Build: Menü *MetaMove → Build Paper Demo APK* |
| `Assets/MetaMove/Scripts/Demo/PinchIkRayController.cs` | Pinch-Erkennung, IK-Target-Drag, Kurvenberechnung |
| `Assets/MetaMove/Scripts/Demo/DemoRobotPlacer.cs` | Platzierung ohne Marker |

Der Ray nutzt Metas eigenes `ReticleLine`-Prefab (`TubeRenderer`) aus dem
Interaction SDK, damit er exakt wie der native Distance-Grab-Ray aussieht.
Metas `DistantInteractionLineVisual` wird dabei entfernt — die verlangt einen
`IDistanceInteractor` und würde den Ray nur beim Zielen auf ein Interactable
zeigen. Die Kurve (kubische Bézier, Hand → TCP) kommt stattdessen aus
`PinchIkRayController`, damit ein Pinch *überall* im Raum den Ray auslöst.

XR-Stack: OpenXR-Loader + Meta XR SDK 85 (`MetaXRFeature`, `HandTracking`,
`MetaQuestFeature`) — kein Oculus-XR-Plugin.

## Bauen und deployen

```bash
# Szene bauen + APK erzeugen (Editor muss geschlossen sein)
"C:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Unity.exe" -batchmode \
  -projectPath c:/git/MetaMove/unity-quest -buildTarget Android \
  -executeMethod MetaMove.EditorTools.PaperDemoBuild.SetupAndBuild \
  -logFile build.log

adb install -r unity-quest/Builds/MetaMovePaperDemo.apk
adb shell am start -n com.MetaMove.PaperDemo/com.unity3d.player.UnityPlayerGameActivity
```

Die Demo hat eine eigene Package-ID (`com.MetaMove.PaperDemo`) und liegt damit
neben der Haupt-App auf der Brille, statt sie zu überschreiben.
