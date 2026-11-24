# HeliSim

HeliSim is a Unity 6 simulator for flying the Robinson R66 helicopter. You can fly at a desk with a keyboard, or use a physical joystick and throttle with a seated OpenXR headset. On an Eight360 NOVA installation, the motion platform can follow the simulated helicopter’s orientation.

[Watch the HeliSim video on YouTube](https://www.youtube.com/watch?v=r83aZbCsfWg)

## What you can do

- **Fly the R66.** Control pitch and roll with the cyclic, change rotor blade pitch with the collective, and control yaw with the pedals. Choose a flight assistance preset before launch and rebind the controls to your keyboard or flight hardware.
- **Fly together or spectate.** Host a direct-IP session and have others join as pilots or spectators. A spectator can follow the flight from a separate display while the pilot uses the cockpit view.
- **Set up a flight dashboard.** Put a cockpit view, external camera, street map, or timing panel on the dashboard. Reassign panels during a session or expand one to full screen.
- **Time and record flights.** Fly the gated time-trial course and use the Timing panel to compare sector splits and lap times. Enable **Record flight data** before launch to save a CSV for analysis afterward.

## Play on Windows

The Windows x64 player includes the helicopter simulation. You can play without installing Unity or buying Silantro.

1. Open the [latest HeliSim release](https://github.com/rylan-person/OS_HeliSim/releases/latest) and download the **Windows-x64.zip** asset.
2. Extract the entire ZIP to a folder and run **OS_HeliSim.exe** from that folder. Keep the executable, data folder, and runtime libraries together.
3. In the main menu, choose **Fly → Start flight** to start a solo session. For seated VR, start your PC OpenXR runtime before launching HeliSim.

For online terrain or street maps, open **Settings → World streaming**. Enter your own Cesium ion token for terrain, Mapbox token for street maps, or both, then select **Save tokens**. HeliSim stores them in Windows Credential Manager for your Windows account. You can start a flight with both fields empty. [Map setup details](docs/MAP_CREDENTIAL_RELEASE.md)

### F8: Switch the main menu between headset and monitor

With your OpenXR headset active and the main menu open, press **F8** to move the menu to the other display. Press **F8** again to switch back. You can also select **Menu on monitor (F8)** or **Menu in headset (F8)** in the main menu. The button names the display the menu will move to.

HeliSim remembers your choice the next time you open the main menu. If your headset disconnects, the menu appears on the monitor and the button reads **Headset unavailable**.

## Run from source in Unity

### 1. Clone the project

Install Git and Git LFS, then run these commands in a terminal. The project uses LFS for aircraft models, textures, and other binary assets.

```sh
git lfs install
git clone https://github.com/rylan-person/OS_HeliSim.git
cd OS_HeliSim
git lfs pull
```

### 2. Purchase and import Silantro

HeliSim depends on Oyedoyin’s **Silantro Helicopter Simulator Toolkit**, Asset Store package **142612**. The package used for this checkout is **3.5.15**. Each developer needs access to a licensed copy through their Unity account. [Get the helicopter toolkit on the Unity Asset Store](https://assetstore.unity.com/packages/tools/physics/silantro-helicopter-simulator-toolkit-142612)

1. In Unity Hub, install Editor **6000.4.5f1** and sign in with the Unity account that owns the toolkit. The repository records its Editor version in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).
2. In Hub, choose **Projects → Add** and select the cloned `OS_HeliSim` folder containing `Assets`, `Packages`, and `ProjectSettings`. Open it with the pinned Editor and allow Unity to restore the project packages. On a fresh clone, missing Silantro types can produce compiler errors until the next step. If Unity offers **Enter Safe Mode**, choose it and continue in Package Manager.
3. In the Editor, open **Window → Package Management → Package Manager**. Select **My Assets**, search for **Silantro Helicopter Simulator Toolkit**, then choose **Download** and **Import**. In the import dialog, keep the toolkit contents selected and confirm **Import**. [Unity’s asset import guide](https://docs.unity3d.com/6000.4/Documentation/Manual/upm-ui-import.html)
4. Keep the imported files at their original paths. Check that `Assets/Silantro/Common/` and `Assets/Silantro/Rotary Wing/` both exist. Allow Unity’s API Updater to update the imported toolkit scripts if prompted, and wait for compilation to finish. The project already supplies its own input and project settings.

The licensed files belong in your **local checkout**. Git ignores `Assets/Silantro/`, its folder metadata, and downloaded `.unitypackage` archives. Keep those files out of commits and source distributions.

### 3. Start your first flight

Open [MainMenu.unity](Assets/Level/Scenes/MainMenu.unity) in the Project window, press **Play**, and select **Fly → Start flight**. Solo and multiplayer flights share the Multiplayer scene. If the Console still reports missing Silantro types, check that both toolkit folders imported into this project before troubleshooting the scene.

To build a Windows player, open **File → Build Profiles**, select the Windows x64 target, and build with the project's enabled scenes in order: **MainMenu**, then **Multiplayer**. For project checks and Editor safety, see [Testing](docs/TESTING.md).
