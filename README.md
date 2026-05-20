# Lit

**Project Summary**
`Lit` is a Unity project built around a squad of characters, systemic interactions, and a UI-first navigation flow (controller-friendly). The project supports both Solo and Multiplayer sessions, and the save/load pipeline keeps those two worlds separated.

**Core Constraint (Multiplayer)**
The game is intended to support multiplayer (up to 4 players, one player per character). All systems and gameplay features should remain compatible with this constraint.

**Scenes**
- `Assets/Scenes/MainMenu.unity`: Entry point and full menu flow (3D title decor, game options, solo/multi, load, new game, virtual keyboard).
- `Assets/Scenes/Maison.unity`: Gameplay scene used for runtime testing and pause menu flow.

**Main Menu Flow**
The main menu is entirely driven by `MainMenuController`.
- `MainMenu_TitleCard`: Title card panel (waits for any input).
- `MainMenu_GameOptions`: Root menu with `Solo`, `Multiplayer`, `Options`, `Quit`.
- `MainMenu_Solo_GameOptions`: Solo submenu with `New Game` and `Load`.
- `MainMenu_Multi_GameOptions`: Multiplayer submenu with `New Game` and `Load`.
- `MainMenu_NewGame`: Shared new game prompt (name input + confirm/cancel).
- `MainMenu_Load`: Shared load screen (sessions list + saves list + details/screenshot).
- `MainMenu_LoadConfirm`: Confirmation for loading a save.
- `MainMenu_VirtualKeyboard`: Shared virtual keyboard for controller text entry.
- `MainMenu_Loading`: Loading overlay while a scene is loading.

**UI Navigation**
The MainMenu uses a mouse-style pointer even when a gamepad is connected.
- `MainMenuPointerCursor`: Drives the visible pointer from mouse or gamepad, projects a torch light into the 3D decor, and sends gamepad pointer clicks to UI.
- `CursorIntercation`: Marks 3D title-decor clue objects; hovering them with the pointer enables an `Outline`.
- `MainMenuTitleDecorController`: Reads the latest save metadata/state and toggles title-decor variants.
- `MenuCursorAction`: Executes menu actions.
- `InputFocusStack`: Ensures only the top-most open panel receives input.
- `LocalInputRouter`: Central routing of input actions with debounce.

**Save & Load System**
Save data is stored under Unity `Application.persistentDataPath` in a `Saves` folder.
- `SaveSessionManager`: Manages sessions and save slots.
- `SaveSessionType`: `Solo` or `Multiplayer` is stored in `session.json` and `meta.json`.
- `CharacterStateStore`: Writes/reads runtime state and triggers screenshots.
- Screenshot file: `screenshot.png` inside each save folder (used in load preview).
- The load menu filters sessions by the current mode (Solo or Multiplayer).

**Solo vs Multiplayer Sessions**
Sessions and saves are separated by `SaveSessionType`.
- Solo creation and loading only list Solo sessions.
- Multiplayer creation and loading only list Multiplayer sessions.
- Starting a Multiplayer session uses host netcode flow; Solo runs offline.

**Multiplayer Runtime**
Multiplayer uses Netcode for GameObjects.
- `NetcodeLauncher`: Starts host/client with connection data.
- `NetcodeSessionCode`: Converts a session code into a port.
- `NetworkInventory` and related netcode components sync gameplay data.

**Pause Menu (Maison)**
`PausePanelController` handles the in-game pause menu.
- `Start` opens the pause panel.
- `SaveButton` uses `MenuCursorAction.Save`.
- `QuitButton` returns to `MainMenu` and shuts down netcode.

**Key Gameplay Systems (Entry Points)**
- Squad: `SquadManager`, `SquadCharacterController`, `SquadAIManager`, `SquadFollowerAgent`.
- Inventory: `InventoryPanelController`, `NetworkInventory`, `LootContainer`, `LootUISettings`.
- Torch: `TorchVisionSystem`, `ToggleTorchEffect`, `TorchEffect`, `TorchLightReceiver`.
- Crafting/Building: `CraftingConstructionPanel`, `BuildingPanelController`, `BuilderController`.
- World/Zone: `Maison`, `Zone`, `Labyrinth`, `HubRosterManager`.
- Skill checks: `SkillCheckSystem`, `SkillCheckFeedback`.

**Audio**
- `AudioClipSO`: Scriptable audio definition.
- `AudioManager`: Central playback helper.

**Configuration Hotspots**
- `MainMenuController`: Menu flow, cursor targets, loading overlay, scene name to load.
- `SaveSessionManager`: Save folder names and metadata file names.
- `LocalInputRouter`: Input debounce duration.

**Removed/Deprecated**
Legacy menu flow and load manager scripts were removed:
- `MainMenuSceneFlow`
- `MainMenuLoadManager`

The old forced MainMenu selection cursor is replaced by the pointer/torch setup installed through `Lit/MainMenu/Install Title Decor`.
