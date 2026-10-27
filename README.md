# Singing Mythica (first pass)

A My Singing Monsters-style game set in the world of Mythica, built in Unity for the Green Bear Labs Arcade (WebGL).

Creatures in Mythica aren't captured; they choose to stand beside you. Each one you raise sings its own part of the island's song.

## What's in this first pass

- **20 creatures** from the Top 20 Story Highlight list in *Mythica Story Creature List*: Golem, Jackalope, Dragon Koi, Ratatoskr, Fire Salamander, Land Shark, Enfield, Cerberus, Wendigo, Kraken, Peryton, Griffin, Phoenix, Manticore, Soleo, Aspidochelone, Pegasus, Hydra, Yeti, Drake.
- **Six magic types** (Fire, Water, Earth, Sky, Light, Shadow). Every creature can carry any of the six, giving 120 variants to discover.
- **Music**: every creature sings a part (drums, bass, arpeggio, melody, choir, bells, and so on). Its magic type changes the instrument sound: Fire is a bright saw, Water is soft and round, Earth is warm, Sky is airy, Light is bell-like, Shadow is dark and detuned. All parts share one tempo and chord progression (Am, F, C, G at 100 BPM), so any combination stays in time and in key. Audio is generated in code; there are no audio files.
- **Coins**: creatures earn coins over time, even while the game is closed (up to one hour's worth each). Tap a creature to collect, or use Collect All.
- **Shop and nest**: buy eggs of a creature's native magic type. Eggs hatch on a timer in the nest (3 slots), then you place them on the island (21 spots). Shop tiers unlock as the island grows (3, 6, 9 creatures).
- **Breeding**, using the rule from the creature list doc: two parents with different magic give a 50% chance of parent A's creature with B's magic, or 50% of parent B's creature with A's magic. Bred (non-native) variants earn 1.5x coins.
- **Bestiary**: a 20 x 6 grid tracking which variants you've found.
- **Creature options**: mute, move, or release back to the wild (for coins).
- **Saves** automatically to the browser (PlayerPrefs, which is IndexedDB on WebGL).
- **Placeholder art**: creatures are built from simple shapes coloured by their magic type, with species features (Cerberus's three heads, the Peryton's antlers, the Kraken's tentacles, the forest on the Aspidochelone's back, and so on).

## Run it in Unity

Tested to compile against Unity 2021.3 APIs. It should work on any Unity from 2021.3 LTS through Unity 6.

1. In Unity Hub, create a new project with the **3D (Built-In Render Pipeline)** or **Universal 3D** template.
2. Copy this `Assets/Scripts` folder into the new project's `Assets` folder.
3. In the editor menu bar choose **Mythica > Create Game Scene**.
4. Press **Play** and click **Start the song**.

The game builds its own island, camera, lighting and UI at runtime, so no prefabs or scene setup are needed. Controls are click or tap, the mouse wheel to zoom, and Esc to cancel.

To start over, use **New game** in the bottom bar (click it twice).

## Build for the Green Bear Labs Arcade (WebGL)

1. Install the **WebGL Build Support** module for your Unity version in Unity Hub (Installs > gear icon > Add modules).
2. Choose **File > Build Settings** (or **Build Profiles** in Unity 6), select **WebGL**, and click **Switch Platform**.
3. Choose **Mythica > Build WebGL (for the Arcade)**. The build goes to `Builds/WebGL` and opens when it's done.
4. Copy the four files in `Builds/WebGL/Build` (`WebGL.loader.js`, `WebGL.data.unityweb`, `WebGL.framework.js.unityweb`, `WebGL.wasm.unityweb`) over the ones in the Arcade repo's `games/singing-mythica/Build/` folder.

From the command line (no editor window), the same build is:

```sh
"/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath . -buildTarget WebGL -executeMethod MythicaBuildMenu.BuildWebGL -logFile -
```

The build uses Gzip compression with a decompression fallback (that's what the `.unityweb` names mean), so it works on any static host.

Browsers block audio until the player clicks, which is why the game opens with a **Start the song** button.

## Where things live

| File | What it does |
| --- | --- |
| `Mythica/MythicaData.cs` | Creature roster, magic types, prices, income, hatch times, breeding rule. Tune the game here. |
| `Mythica/MusicSynth.cs` | Each part's note pattern, the instrument sound for each magic type, and the shared music clock. |
| `Mythica/CreatureBuilder.cs` | Placeholder creature models. Swap for real art later. |
| `Mythica/Creature.cs` | A placed creature: singing, bouncing on its notes, earning coins. |
| `Mythica/Island.cs` | The island, the six magic crystals, and the 21 placement spots. |
| `Mythica/GameManager.cs` | Game rules: shop, nest, placing, breeding, collecting, saving. Boots itself in any scene. |
| `Mythica/GameUI.cs` | The on-screen UI (Unity IMGUI). |
| `Mythica/SaveSystem.cs` | Save data and browser storage. |
| `Editor/MythicaBuildMenu.cs` | The Mythica menu: create the scene and build for WebGL. |

## Good next steps

- Real creature art: replace `CreatureBuilder.Build` with prefabs per species (and tinted materials per magic type).
- Recorded or composed loops per creature, replacing the generated tones.
- A real UI (uGUI or UI Toolkit) styled to match the arcade site.
- The ideas in the creature list doc: limited breeding per adult, releasing creatures to the wild, and unlocking species by encountering them.
- More islands, one per magic type, each with its own song.
- Decide whether Enfield is Sky (Top 20 list) or Earth (breeding table). The game currently uses Sky.
