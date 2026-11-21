# Starfall: Prototype Configuration & Flight Directives Walkthrough

### 1. Pre-Game Flight Directives & Controls Overlay
- **Automatic Mission Briefing**: Before combat starts in Level 1 (or any deployed mission), a tactical **Mission Flight Directives** modal appears:
  - **Flight Propulsion**: `[W] [A] [S] [D]` / `[ARROW KEYS]` / `[MOUSE DRAG]` (Maneuver and bank warship)
  - **Primary Cannons**: Automatic synchronized convergence fire
  - **Tactical Screen Bomb**: `[SPACEBAR]` / `[B KEY]` / `[RIGHT MOUSE CLICK]` (Cancels all enemy bullets & blasts screen)
  - **Special Ability**: `[E KEY]` / `[HUD ABILITY BUTTON]` (Phase Shift, EMP Surge, Plasma Overdrive, etc.)
  - **Command HQ / Pause**: `[ESC KEY]` / `[HQ BUTTON]`
  - **Controls Toggle**: `[H KEY]` or clicking `[?] KEYS` on the top-right combat HUD opens/closes directives anytime.
- **Player Preferences**:
  - Includes a `[✓] SHOW BEFORE LAUNCH` checkbox button. Players can toggle it on or off, and the preference is saved persistently via `PlayerPrefs`.
  - Pressing `[SPACE]` or clicking `[COMMENCE MISSION]` resumes time and launches the wave battle.

---

### 2. Full Campaign Sector Access & Victory Sequence
- **Playable Sectors**:
  - **Sector 01 (Earth Orbit)**: 10 Waves • Hazard: Asteroids • Boss: Invader Carrier
  - **Sector 02 (Asteroid Belt)**: 10 Waves • Hazard: Debris Crossfire • Boss: Plasma Dreadnought
  - **Sector 03 (Deep Nebula)**: 10 Waves • Hazard: Nebula Swarms • Boss: Hive Queen Leviathan
- **Dynamic Campaign Progression**:
  - Sectors 1, 2, and 3 are now fully operational in the Command Hub with dynamic wave matrix jump selectors (Waves 1-10).
  - Defeating Sector 01 engages the warp drive and automatically advances to Sector 02.
  - Defeating Sector 02 engages the warp drive and automatically advances to Sector 03.
  - Defeating the Hive Queen in Sector 03 triggers the full **CAMPAIGN VICTORY** celebration screen.
  - Developers can also quickly unlock all sectors, unlock all waves, or reset progress using the Unity menu under **`STARFALL -> Campaign Sectors`**.

---

### 3. Automated itch.io Build Pipeline
Located in the Unity Editor top menu under **`STARFALL -> Build Prototype`**:
- **Build Windows Standalone (64-bit)**: Generates 64-bit `.exe` and creates `Starfall_Windows_itch.zip`.
- **Build WebGL (itch.io HTML5)**: Generates WebGL HTML5 package and creates `Starfall_WebGL_itch.zip` with uncompressed decompression fallback for itch.io browser compatibility.
- **Build Both Windows & WebGL**: Executes both builds sequentially.
- **Open Builds Folder**: Opens `Alienwar/Builds/` in File Explorer.
