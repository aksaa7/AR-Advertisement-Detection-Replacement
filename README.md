# AR Advertisement Detection & Replacement

Mixed-reality app for **Meta Quest 3 / 3S** that detects advertisements (screens, billboards) in the real world and either **blurs them** or **replaces them with a relaxing beach video**. Detection runs on-device with a **YOLOv9** model via **Unity Inference Engine (Sentis)** on images from Meta's **Passthrough Camera API**.

The project was built as an application project in Software Engineering at Ulm University and includes a user study (N = 18) comparing three conditions: original ads, blurred ads and replaced ads.

Based on Meta's [Unity-PassthroughCameraApiSamples](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples) (original sample README kept as `README 2.md`).

---

## How it works

1. The **Passthrough Camera API** delivers the headset's camera image.
2. **Sentis** runs the YOLOv9 model on each frame and returns bounding boxes.
3. `TvOverlayController` picks the best box for the target class (`tvmonitor`), waits until it is stable and **locks a blur overlay** onto it in world space.
4. When a navigation button is pressed, `CanvasToggler` swaps the blur for the **beach video** (`showVideo = true`) or for a direction arrow (`showVideo = false`).

### Controls (Quest controllers)

| Button | Action |
|---|---|
| **A** | Show "left" direction / video |
| **B** | Show "right" direction / video |
| **X** | Show "straight ahead" direction / video |
| **Y** | Show "U-turn" direction / video |
| Same button again | Hide it again and go back to the blur |

---

## User study

Within-subject study with **18 participants** comparing **A** original ads, **B** blurred ads and **C** ads replaced by a relaxing video. Measures: SUS, NASA-TLX, Trust in Automation, ARI immersion and the error rate on a distraction task.

| Measure | A Original | B Blurred | C Replacement | p |
|---|---|---|---|---|
| SUS (usability) | 75.97 | 88.33 | 85.56 | 0.0002 (Friedman) |
| NASA-TLX (workload) | 15.46 | 8.15 | 2.49 | < 0.0001 (Friedman) |
| Trust in Automation | 3.87 | 4.07 | 3.97 | 0.0779 (RM-ANOVA) |
| ARI (immersion) | 3.47 | 5.59 | 5.89 | < 0.0001 (Friedman) |
| Error rate (%) | 25.94 | 21.00 | 17.86 | 0.0010 (Friedman) |

Blurring and replacing ads both improved usability and immersion and reduced workload and errors; the relaxing video worked best. All LimeSurvey data, the analysis script (Friedman / RM-ANOVA with Holm-corrected post-hoc tests), plots and the final presentation are in **[`Study/`](Study/)**.

---

## Requirements

- **Unity 6** – `6000.0.69f1` (see `ProjectSettings/ProjectVersion.txt`)
- **Meta MR Utility Kit** `85.0.0` and **Unity Inference Engine** `2.5.0` (installed automatically from `Packages/manifest.json`)
- **Meta Quest 3 / 3S** with Horizon OS v74+ – passthrough camera access does **not** work in the XR Simulator or over Link
- Android permission `horizonos.permission.HEADSET_CAMERA`

## Getting started

1. Clone the repo and open the folder in **Unity Hub** (Unity 6000.0.69f1).
2. Unity rebuilds the `Library/` folder on first open – this takes a few minutes.
3. Open the main scene **`Assets/PassthroughCameraApiSamples/MultiObjectDetection/MultiObjectDetection.unity`**.
4. *File → Build Profiles* → platform **Android** → connect the Quest → **Build And Run**.
5. Allow camera access on the headset when asked.

---

## Folder structure

```
AR-Advertisement-Detection-Replacement/
├── Assets/
│   ├── PassthroughCameraApiSamples/      ← main project code
│   │   ├── MultiObjectDetection/         ← ★ main scene + ad detection & replacement
│   │   ├── PassthroughCamera/            ← camera access, permissions, input
│   │   ├── StartScene/                   ← start menu / debug UI (from Meta sample)
│   │   ├── CameraToWorld/                ← Meta sample scene (unchanged)
│   │   ├── BrightnessEstimation/         ← Meta sample scene (unchanged)
│   │   └── ShaderSample/                 ← Meta sample scene (unchanged)
│   ├── Sickscore Games/
│   │   └── HUD-Navigation-System/        ← navigation HUD asset + own scripts/prefabs, beach video
│   ├── beffio/                           ← UI Builder asset (fonts, panel graphics)
│   ├── Resources/                        ← Meta/OVR runtime & build settings
│   ├── Oculus/, MetaXR/, XR/             ← Meta XR / OpenXR project settings
│   └── Plugins/Android/                  ← AndroidManifest.xml (camera permission)
├── Packages/                             ← Unity package list (manifest.json)
├── ProjectSettings/                      ← Unity project settings, build scenes
├── Media/                                ← GIFs for the original sample README
├── Study/                                ← ★ user study: data, analysis script, results, presentation
├── README.md                             ← this file
└── README 2.md                           ← original Meta sample README
```

### Main folder in detail: `Assets/PassthroughCameraApiSamples/MultiObjectDetection/`

| Path | Contents |
|---|---|
| `MultiObjectDetection.unity` | **Main scene** – the only scene enabled in the build |
| `SentisInference/Model/` | YOLOv9 model (`yolov9onnx.onnx`, `yolov9sentis.sentis`) |
| `SentisInference/Scripts/` | Running the model (`SentisInferenceRunManager`), drawing boxes (`SentisInferenceUiManager`), `SentisToTvOverlayBridge` |
| `DetectionManager/Scripts/TvOverlayController.cs` | **Own script:** finds the ad (class `tvmonitor`), locks and positions the blur overlay |
| `DetectionManager/Scripts/` | Detection UI, markers, menu (from Meta sample, adapted) |
| `DetectionManager/Prefabs/` | `Blur.prefab` (overlay), `Direction.prefab`, detection UI |
| `BackgroundBlur.shader`, `Blurr.mat` | Blur shader and material for the overlay |
| `Navigation.prefab` | Navigation overlay prefab |
| `UIController.cs` | **Own script:** shows UI only while a "TV" is detected |
| `RandomObjectSpawner.cs` | **Own script:** shows a random direction with a random distance (A button) |
| `EnvironmentRaycast/` | Places objects on real surfaces via MRUK raycasts |
| `CameraViewer/` | Simple camera-preview scene (Meta sample) |

### `Assets/Sickscore Games/HUD-Navigation-System/`

Third-party **HUD Navigation System** asset (compass, minimap, indicators – see `Documentation.pdf`) plus own additions:

| Path | Contents |
|---|---|
| `Scripts/CanvasToggler.cs` | **Own script:** switches blur ↔ video / direction arrow with A/B/X/Y |
| `Scripts/OnEnableDisable.cs` | **Own script:** fires events when an object is enabled/disabled |
| `Scripts/AdvertismentPlacement.prefab` | Ad-placement prefab |
| `_Examples/Textures/Icons/` | Direction icons (`direction*.png`), **beach video** (`Relaxing … Palm Tree.mp4`) and the `RenderTexture` it plays into |
| `_Examples/ExampleScene/` | Asset demo scenes (not part of the app) |

---

## Not included in the repo

These are ignored via `.gitignore` and are created locally or kept elsewhere:

- `Library/`, `Logs/`, `Temp/`, `obj/`, `UserSettings/` – generated by Unity
- `*.apk` – build outputs
- `*.rar` – local project backups
- The beach video is a **30-minute cut** of the original 3-hour video so it fits GitHub's 100 MB file limit; set the VideoPlayer to **Loop** for longer sessions.

## License

Code based on Meta's Passthrough Camera API Samples is under the license in `LICENSE.txt`. Third-party assets (Sickscore Games HUD Navigation System, beffio UI Builder) are subject to their own Unity Asset Store licenses.
