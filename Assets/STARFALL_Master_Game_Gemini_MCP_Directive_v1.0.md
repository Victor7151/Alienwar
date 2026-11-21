# STARFALL — MASTER GAME + GEMINI MCP PRODUCTION DIRECTIVE
## Version 1.0 — Master Source of Truth

**Project:** STARFALL  
**Genre:** 2D arcade space shooter  
**Platform:** Android first  
**Engine:** Unity  
**Development assistant:** Gemini MCP  
**Character/art pipeline:** Adobe Illustrator → exported game assets → Unity  
**UI:** Figma  
**Selected animation/UI work:** Rive where appropriate  
**Story cinematics:** Google Flow + video editor  
**Status:** Pre-production / production planning

---

# 0. PURPOSE

This is the master production directive for STARFALL. It defines what the game is and tells Gemini MCP how to help build it without inventing conflicting systems, silently changing the design, or creating an unmaintainable Unity project.

Gemini must treat this document as the primary game-design source of truth. If another instruction, temporary idea, code comment, or generated asset conflicts with this document, identify the conflict before making a major change.

This is a living specification. Final decisions should be recorded here or in the relevant supporting document.

# 1. GAME IDENTITY

STARFALL is a fast, funny, cinematic 2D arcade space shooter. Humanity is attacked by strange, colorful alien creatures known as the Zupps. The invasion initially appears to be a simple alien conquest. The player becomes a human space pilot and fights through enemy formations, environmental hazards, alien worlds, warp events and bosses. As the campaign progresses, the player discovers that the Zupps are connected to a much larger threat.

Core features:
- Arcade shooting
- Touch movement
- Automatic firing
- Enemy waves
- Dodging
- Hazards
- Power-ups
- Weapons
- Ship upgrades
- Missions
- Boss fights
- Warp events
- Story cinematics
- Humor and mystery

Core loop:

**MOVE → SHOOT → DODGE → COLLECT → SURVIVE → DEFEAT → REWARD → UPGRADE → DISCOVER**

# 2. ORIGINAL-IP RULE

STARFALL may be inspired by the nostalgia and broad genre of classic space shooters, but it must remain an original IP. Do not copy Chicken Invaders or any other game's characters, names, story, dialogue, artwork, UI, sounds, music, exact enemy designs, levels, logos or branding. Broad genre mechanics such as shooting enemies, asteroids, power-ups, bosses and warp travel are general concepts; STARFALL-specific implementation and content must be original.

# 3. VISUAL IDENTITY

Premium stylized 2D sci-fi arcade art.

Prioritize:
- Strong silhouettes
- Clean readable shapes
- Expressive characters
- Bold color separation
- Vibrant VFX
- Layered parallax backgrounds
- Clear projectile readability
- Satisfying explosions

Avoid generic stock sci-fi, photorealism, excessive visual noise, unreadable particles, and inconsistent art styles between worlds.

# 4. SOFTWARE PIPELINE

**Adobe Illustrator — primary character/asset art**
- Zupp characters
- Enemies
- Player ship
- Bosses
- Weapons
- Props
- Character turnarounds
- Separated body parts

**Figma — UI/design**
- HUD
- Menus
- Mission screens
- Upgrade screens
- World selection
- Buttons
- Icons
- Marketing layouts

**Photoshop — optional** for raster editing, texture treatment and cleanup when genuinely required.

**Rive — selective animation** for reusable UI motion and suitable small effects; do not force Rive into systems better handled by Unity.

**Unity — runtime** for gameplay, levels, animation integration, save/progression and final game behavior.

# 5. ZUPP SPECIES

Working name: **Zupps**.

Zupps are funny, expressive, chaotic and technologically advanced. They are not inherently evil. Their motives become clearer through the story.

Personality:
- Funny
- Expressive
- Chaotic
- Intelligent
- Competitive
- Occasionally foolish
- Dangerous when fighting

Humor should come from behavior, animation and situations, not constant jokes.

# 6. ZUPP SCOUT — CANONICAL FIRST CHARACTER

The approved Zupp Scout reference sheet is the master visual reference for the first enemy and establishes the visual language for the Zupp species.

Design:
- Large orange/yellow rounded head
- Large expressive eyes and dark pupils
- Wide expressive mouth and small teeth
- Two antennae
- Compact armored body
- Dark navy/purple armor
- Cyan energy details
- Orange exposed limbs/hands
- Purple/navy boots
- Backpack/energy unit
- Compact plasma weapon
- Blue/cyan weapon glow

Reference views:
- Front
- 3/4
- Side
- Back

Expressions:
- Neutral
- Angry
- Surprised
- Scared
- Mischievous

Recommended modular parts:
- Head
- Eyes
- Pupils
- Mouth
- Teeth
- Antenna L/R
- Body/armor
- Chest core
- Arms
- Hands
- Legs
- Boots
- Backpack
- Weapon
- Thruster
- Hit effect
- Death effect

# 7. PLAYER

The player is primarily represented by a human combat spacecraft during gameplay. The human pilot appears mainly in the opening cinematic, story scenes, briefings, hangar, victory/defeat and important narrative moments.

The ship must have a strong recognizable silhouette and remain visually consistent across gameplay and cinematics.

# 8. CORE GAMEPLAY

Perspective: 2D horizontal arcade shooter.

Primary control proposal:
- Touch-and-drag movement
- Responsive movement
- Automatic weapon firing
- Special ability button
- Clear movement boundaries

The player should not need to repeatedly tap to fire during standard gameplay.

Combat actions:
- Move
- Fire
- Dodge
- Collect power-ups
- Activate special ability
- Defeat waves
- Defeat bosses
- Earn rewards

# 9. PLAYER SYSTEMS

Initial systems:
- PlayerController
- WeaponSystem
- HealthSystem
- ShieldSystem
- SpecialAbilitySystem
- PowerUpSystem
- ScoreSystem
- ComboSystem
- ShipUpgradeSystem

Keep systems modular. Do not create one enormous Player script containing every feature.

# 10. HEALTH AND SHIELD

**Shield:** absorbs incoming damage and may recharge after avoiding damage for a configurable period.

**Hull:** permanent mission health. At zero, the player dies unless a permitted revive is used.

Values must be configurable and data-driven.

# 11. WEAPONS

Initial concepts:
1. Plasma Shot
2. Triple Shot
3. Flame Burst
4. Laser Beam
5. Missile Storm
6. Star Cannon

Each weapon should expose tunable data such as damage, fire rate, projectile count, projectile speed, range, lifetime, area damage, tracking and energy usage.

# 12. POWER-UPS

Initial categories:
- Weapon upgrade
- Shield restore
- Hull repair
- Fire-rate boost
- Temporary overdrive
- AOE bomb
- Score multiplier
- Special-energy recharge

Each needs clear visuals, pickup feedback, audio and a configurable gameplay effect.

# 13. SPECIAL ABILITY

The player has a rechargeable special ability. It should charge through gameplay, have tactical value, produce strong VFX and not make normal combat irrelevant. Final ability is selected during prototype testing.

# 14. SCORE, COMBO AND CURRENCY

Enemies award score. Continuous successful play may increase a combo multiplier such as x2 → x3 → x4 → x5. The formula must be tunable.

Working currency: **Star Credits**.

Earn through missions, bosses, challenges, high scores, daily rewards and optional rewarded ads. Use primarily for progression/upgrades.

# 15. SHIP UPGRADES

Potential categories:
- Weapon damage
- Fire rate
- Shield capacity
- Hull health
- Movement speed
- Energy capacity
- Special ability strength
- Critical chance

Balance upgrades against campaign difficulty.

# 16. ENEMY ROSTER

Initial enemies:

**Scout:** fast, low health, basic enemy.

**Shooter:** keeps distance and fires projectiles.

**Bomber:** controls space with explosives.

**Charger:** locks onto the player and rushes.

**Shielder:** protects nearby enemies.

**Tank:** high health and heavy attacks.

**Teleporter:** disappears and reappears.

**Swarm:** many small enemies.

**Elite:** stronger specialized variants.

Every enemy requires a purpose, silhouette, movement, attack behavior, health, damage, telegraph, hit reaction, death behavior, score and reward/drop behavior.

Do not make enemies difficult only by giving them huge health. Difficulty should come from positioning, patterns, timing, formations, hazards and enemy combinations.

# 17. LEVEL ARCHITECTURE

Levels should be data-driven whenever practical.

A mission should define:
- World ID
- Mission ID
- Name
- Description
- Background
- Music
- Difficulty
- Wave list
- Spawn configuration
- Hazards
- Power-ups
- Objectives
- Mini-boss/boss
- Cinematic before/after
- Rewards
- Unlock requirements
- Completion conditions

Recommended flow:

**LevelData → LevelManager → WaveManager → EnemySpawner**

Do not hard-code every mission into custom scene scripts.

# 18. STANDARD MISSION STRUCTURE

A typical mission can use:

**Intro → Wave 1 → Wave 2 → Hazard introduction → Wave 3 → Power-up opportunity → Advanced wave → Elite/Mini-boss → Special event/Warp → Final wave → Boss/extraction → Victory → Rewards**

Not every mission must contain every stage. Variation is required.

# 19. LEVEL DESIGN DIRECTIVE FOR GEMINI

Before implementing any level, Gemini must:

1. Read the GDD and relevant existing systems.
2. State the mission's gameplay purpose.
3. Create a wave/pacing plan.
4. Identify new mechanics being taught.
5. Identify enemy combinations.
6. Define hazards and their timing.
7. Define the finale.
8. Implement through existing data-driven systems.
9. Test spawn timing, collisions, difficulty, completion and rewards.
10. Document the mission.

Use pacing such as:
**Calm → Build → Pressure → Release → Escalation → Finale**.

Do not introduce multiple major mechanics at once unless the mission is deliberately advanced.

# 20. CAMPAIGN WORLDS

## WORLD 1 — EARTH

Purpose: teach the game.

Visuals: Earth, clouds, Moon, satellites, stations, debris, stars and city lights.

Hazards: asteroids, debris, missiles, energy fields.

Story: the invasion begins.

### Earth mission roadmap

**Mission 1 — First Contact**
- Movement
- Shooting
- Basic Scout

**Mission 2 — Incoming**
- Enemy formations
- Shooter enemy

**Mission 3 — Orbital Defense**
- Asteroids
- Projectile pressure

**Mission 4 — Breaking Point**
- Mixed enemy groups
- Higher pressure

**Mission 5 — Starfall**
- Starfall facility
- Elite/mini-boss
- Narrative reveal

**Mission 6 — The Warp**
- Final Earth battle
- Starfall activation
- Warp to next world

This is the initial target, not an immutable count.

## WORLD 2 — PYRAX

Volcanic alien world.

Visuals: lava, volcanoes, smoke, glowing rock, alien structures.

Hazards: lava bursts, falling rocks, fireballs, volcanic explosions.

Boss: **Magma Machine**.

## WORLD 3 — FROSTARA

Frozen alien world.

Visuals: ice mountains, snow, aurora, crystals, frozen structures.

Hazards: ice chunks, ice storms, freezing fields, large ice formations.

Boss: **Frost Warden**.

## WORLD 4 — NEXUS

Planet-sized artificial technology world.

Visuals: giant machines, energy towers, futuristic structures, portals and mechanical cities.

Hazards: laser gates, energy walls, gravity fields and teleportation systems.

Boss: **Core Guardian**.

## WORLD 5 — OUTER RIFT

Unstable space.

Visuals: nebulas, wormholes, space fractures, distorted stars and debris.

Core mechanic: warp events, teleporting enemies and changing battle conditions.

Boss: Rift-related boss, final design TBD.

## WORLD 6 — THE VOID

Final region and source of the mystery.

Visuals: dark space, massive structures, strange energy and reality distortions.

The final threat is visually distinct from the Zupps.

# 21. WARP SYSTEM

Warp is a signature STARFALL feature.

Sequence:
1. Warning
2. Energy buildup
3. Screen distortion
4. Sound/music transition
5. Portal formation
6. Player transport
7. New environment
8. Gameplay resumes

Warp can be a world transition, story event, gameplay event or surprise encounter.

Designed missions should use controlled/deterministic warp behavior so it remains fair.

# 22. BOSSES

Every boss should have:
- Strong silhouette
- Intro
- Health system
- Multiple phases
- Telegraphs
- Distinct attacks
- Optional destructible components
- Damage states
- Defeat sequence
- Rewards

Suggested structure:
**Phase 1 → Phase 2 → Phase 3 → Core exposure → Final attack → Defeat**.

Bosses must teach recognizable patterns rather than relying on arbitrary difficulty.

Initial bosses:
- Earth: Invader Carrier
- Pyrax: Magma Machine
- Frostara: Frost Warden
- Nexus: Core Guardian
- Outer Rift: TBD
- The Void: Final Threat / TBD

# 23. STORY

## Act I — The Invasion

Humanity detects an approaching fleet. The Zupps attack Earth. Human defenses begin collapsing. The player launches an experimental spacecraft.

## Act II — The Chase

The player follows the invaders through space and is transported beyond the solar system.

## Act III — The Mystery

Evidence suggests the invasion is not what humanity believed. The Zupps are running from something.

## Act IV — The Alliance

Humanity and the Zupps may have to cooperate against a greater threat.

## Act V — The Void

The player reaches the source of the disturbance and confronts the true threat.

Story should be revealed gradually through cinematics, mission events and environmental discoveries.

# 24. STORY/GAMEPLAY INTEGRATION

Story must not unnecessarily interrupt gameplay.

Use story primarily for:
- Opening
- Mission briefings
- World transitions
- Major discoveries
- Boss introductions
- Boss defeats
- Major reveals
- Ending

Most missions remain gameplay-focused.

# 25. GOOGLE FLOW CINEMATIC SYSTEM

Google Flow is used for story/cinematic clips. Because generated clips are short, every cinematic must be treated as a sequence of modular shots rather than one long generation.

Pipeline:

**Story beat → Shot list → Continuity specification → Individual Flow prompts → Generate clips → Select approved takes → Edit → Add sound/music/dialogue → Export → Import into Unity**

Target each generated shot at approximately 5–10 seconds.

A complete cinematic may therefore be:

Shot 01 → Shot 02 → Shot 03 → Shot 04 → Shot 05 → final edit.

# 26. GOOGLE FLOW SHOT SPECIFICATION

Every shot must have:
- Shot ID
- Story purpose
- Duration target
- Characters
- Canonical character reference
- Environment
- Action
- Camera position/framing
- Camera movement
- Lighting
- Art direction
- Continuity requirements
- Ending state

Example ID:
`EARTH_INTRO_001`

The ending frame of one shot should make the next shot easy to connect.

# 27. OPENING CINEMATIC PLAN

Initial modular sequence:
1. Peaceful Earth from space.
2. Earth night side.
3. Astronomers monitor deep-space signals.
4. Strange objects appear on radar.
5. Objects change direction toward Earth.
6. Scientists realize they are controlled.
7. Emergency preparations begin.
8. First strange craft enters atmosphere.
9. More craft appear.
10. First Zupp creature reveal.
11. Fleet approaches Earth.
12. Human defense fleet launches.
13. Large battle.
14. Human fleet begins losing.
15. Starfall facility activates.
16. Alien technology reacts to Starfall.
17. Experimental player ship reveal.
18. Player ship launches.
19. Player enters battle.
20. Title reveal.
21. Gameplay begins.

These are story beats, not final prompts.

# 28. CINEMATIC CONTINUITY BIBLE

Maintain canonical references for:
- Player ship
- Human pilot
- Zupp Scout
- General Blob
- Zupp species
- Bosses
- Earth
- Pyrax
- Frostara
- Nexus
- Outer Rift
- The Void

Recurring elements must preserve design, proportions, colors, equipment, scale and environment identity.

Do not allow generated clips to randomly redesign approved characters or ships.

# 29. CINEMATIC FILE ORGANIZATION

Recommended:

Documentation/Cinematics/
- Opening/
- Earth/
- Pyrax/
- Frostara/
- Nexus/
- OuterRift/
- TheVoid/
- Ending/

Production/Flow/
- References/
- Prompts/
- Generated/
- Approved/
- Rejected/

Unity:
Assets/Video/Cinematics/
- Opening/
- World01/
- World02/
- etc.

Only approved final clips should enter Unity.

# 30. UNITY CINEMATIC SYSTEM

Cinematics should be data-driven where practical.

A cinematic record can contain:
- Cinematic ID
- Video clip
- Trigger condition
- Skip setting
- Resume scene
- Audio settings
- Subtitle/text data if required

Do not hard-code story video playback into unrelated gameplay scripts.

Cinematics should generally be skippable after the first viewing. Save the watched state.

# 31. STORY EVENT FLOW

World transition example:

**Mission complete → Rewards → Story trigger → Cinematic → World unlock → World selection**

Boss example:

**Warning → Boss intro → Boss gameplay → Boss defeat → Rewards → Optional story cinematic**

Keep boss intros short enough that the player is not forced to wait unnecessarily before gameplay.

# 32. MISSION DATA

Each mission data record should contain at least:
- Mission ID
- World ID
- Display name
- Description
- Background
- Music
- Difficulty
- Wave list
- Hazard list
- Objectives
- Boss/miniboss
- Reward
- Unlock requirements
- Pre-cinematic
- Post-cinematic
- Completion state

Example:

`EARTH_01`

Purpose: Tutorial

Waves: `EARTH_01_W01`, `EARTH_01_W02`, `EARTH_01_W03`

# 33. WAVE DATA

Each wave should define:
- Wave ID
- Start delay
- Enemy type
- Quantity
- Spawn positions
- Spawn intervals
- Formation
- Movement pattern
- Attack configuration
- Optional elite
- Completion condition

Avoid hard-coding waves into giant scripts.

# 34. FORMATIONS

Initial patterns:
- Horizontal line
- Vertical line
- V formation
- Arc
- Zig-zag
- Diagonal
- Circle
- Swarm
- Dive formation
- Mixed formation

Parameters should be tunable.

# 35. SPAWNING AND POOLING

Use object pooling for frequently spawned objects where practical:
- Enemies
- Projectiles
- Enemy projectiles
- Explosions
- Hit effects
- Power-ups
- Damage effects

Avoid repeated Instantiate/Destroy during heavy combat where pooling is appropriate.

# 36. SAVE SYSTEM

Persist:
- Unlocked worlds
- Unlocked missions
- Completed missions
- Best scores
- Currency
- Upgrade levels
- Weapon unlocks
- Cinematic watched state
- Settings
- Other permanent progression

Permanent progression must not depend only on scene state.

# 37. UI

Core screens:
- Loading
- Main menu
- World selection
- Mission selection
- Story/cinematic
- Gameplay HUD
- Pause
- Victory
- Defeat
- Rewards
- Hangar
- Ship upgrades
- Weapon upgrades
- Missions/challenges
- Settings

HUD should prioritize:
- Hull
- Shield
- Score
- Combo
- Weapon level
- Special charge
- Mission objective
- Pause

# 38. AUDIO AND VFX

Audio categories:
- Weapons
- Enemy attacks
- Hits
- Explosions
- Shield impact
- Power-ups
- Warp
- Boss warning
- Boss attacks
- UI
- Victory/defeat
- Story

VFX categories:
- Muzzle flash
- Projectile trail
- Hit sparks
- Enemy destruction
- Shield impact/recharge
- Power-up glow
- Player damage
- Explosions
- Boss damage
- Warp distortion
- Special ability
- Environmental hazards

# 39. PERFORMANCE

Android performance is a core requirement.

Consider:
- Object pooling
- Sprite atlases
- Texture compression
- Particle limits
- Efficient collision
- Avoiding unnecessary Update calls
- Avoiding repeated scene searches
- Avoiding unnecessary allocations
- Controlled enemy counts
- Efficient UI updates
- Video memory
- Lower-end Android testing

Measure performance rather than optimizing blindly.

# 40. UNITY ARCHITECTURE

Proposed modular systems:

Core:
- GameManager
- SceneLoader
- SaveManager
- AudioManager
- VFXManager
- PoolManager

Gameplay:
- PlayerController
- WeaponSystem
- ProjectileSystem
- EnemyController
- EnemySpawner
- WaveManager
- LevelManager
- HazardSystem
- BossController

Progression:
- ScoreSystem
- ComboSystem
- CurrencySystem
- UpgradeSystem
- MissionSystem
- UnlockSystem

Story:
- CinematicManager
- StoryManager
- Dialogue/Subtitles if needed

UI:
- UIManager
- HUDController
- MissionUI
- UpgradeUI
- RewardUI
- WorldMapUI

Do not create all systems at once.

# 41. DATA ARCHITECTURE

Use ScriptableObjects or another simple data-driven approach for content requiring frequent tuning.

Likely data objects:
- EnemyData
- WeaponData
- PowerUpData
- BossData
- MissionData
- WaveData
- WorldData
- UpgradeData
- ShipData
- CinematicData

Avoid over-engineering.

# 42. PROJECT STRUCTURE

Assets/
- Art/Characters/Player
- Art/Characters/Zupps
- Art/Ships
- Art/Enemies
- Art/Bosses
- Art/Worlds
- Art/Backgrounds
- Art/Props
- Art/VFX
- Art/UI
- Audio/Music
- Audio/SFX
- Animations
- Prefabs/Player
- Prefabs/Enemies
- Prefabs/Projectiles
- Prefabs/PowerUps
- Prefabs/Bosses
- Prefabs/Hazards
- Prefabs/UI
- Scenes/Boot
- Scenes/Menus
- Scenes/Worlds
- Scripts/Core
- Scripts/Player
- Scripts/Combat
- Scripts/Enemies
- Scripts/Bosses
- Scripts/Levels
- Scripts/Hazards
- Scripts/Progression
- Scripts/Story
- Scripts/UI
- Scripts/Audio
- Scripts/Save
- ScriptableObjects
- Video/Cinematics
- Documentation

Adjust when the actual project benefits from a better structure; do not create duplicate folders just to follow the template.

# 43. MONETIZATION

Preferred approach:
- Free-to-play
- Optional rewarded advertisements
- Optional revive
- Optional bonus reward
- Cosmetic content where appropriate
- Optional premium content later

Avoid forced ads after every mission, pay-to-win progression and excessive interruptions.

# 44. DEVELOPMENT PHASES

## Phase 1 — Design lock
Complete GDD, character bible, ship concept, enemy concepts, world concepts, technical architecture and cinematic bible.

## Phase 2 — Core prototype
Player ship + touch movement + auto-fire + one projectile + one enemy + spawning + collision + damage + death.

## Phase 3 — Combat prototype
Multiple enemies + projectiles + shield + health + power-ups + asteroids + score + combo.

## Phase 4 — Boss prototype
Boss + multiple phases + attacks + health + defeat sequence.

## Phase 5 — Earth vertical slice
Polished Earth section with several enemies, hazards, power-ups, UI, VFX, audio, mini-boss, boss, rewards and story transition.

## Phase 6 — Progression
Missions + currency + upgrades + save + unlocks.

## Phase 7 — Story pipeline
Opening + mission story beats + world transitions + boss intros + major reveals.

## Phase 8 — Content expansion
Additional worlds and bosses.

## Phase 9 — Polish
Balance, optimize, test and improve.

# 45. VERTICAL SLICE RULE

Do not build six worlds before proving one world works.

Earth is the quality benchmark.

The vertical slice must answer:
- Is movement fun?
- Is shooting satisfying?
- Are enemy patterns readable?
- Are hazards fun?
- Are power-ups useful?
- Is the boss exciting?
- Does the story feel integrated?
- Is the art coherent?
- Does the game perform well on Android?

# 46. GEMINI MCP — OPERATING RULES

Gemini is the development assistant, not the game director.

1. Read this document before major implementation.
2. Inspect the existing Unity project before changing code.
3. Identify reusable systems and avoid duplicates.
4. Do not silently redesign the game.
5. For major design changes, explain the issue and propose alternatives.
6. Build incrementally.
7. Compile and test after meaningful changes.
8. Preserve working functionality.
9. Keep systems modular and data-driven.
10. Prioritize Android performance.
11. Do not claim a feature works unless it was actually verified.
12. Report files created/changed, what changed, how to test and what remains unverified.

# 47. GEMINI MCP — LEVEL CREATION COMMAND

When asked to create a level, Gemini must:

1. Read the GDD and relevant level/enemy systems.
2. Inspect LevelManager, WaveManager, EnemySpawner and existing data.
3. State the mission purpose.
4. Produce a wave and pacing plan.
5. Identify what the player is learning or being challenged by.
6. Implement using the existing data-driven architecture.
7. Test timing, spawning, collisions, difficulty, objectives and rewards.
8. Check performance.
9. Document the level.

Never create a completely separate hard-coded level system for one mission unless explicitly approved.

# 48. GEMINI MCP — STORY COMMAND

When asked to add story:

1. Identify the narrative purpose.
2. Identify when it occurs.
3. Classify it as opening, briefing, mid-mission, post-mission, transition, boss intro, boss defeat or ending.
4. Create a cinematic specification.
5. Create a modular shot list.
6. Create separate Google Flow prompts for each shot.
7. Store generated clips in the production area.
8. Move only approved final clips into Unity.
9. Connect the cinematic to the appropriate game event using CinematicData/CinematicManager or the established equivalent.
10. Make it skippable after first viewing where appropriate.
11. Test first playback, repeat playback and skip behavior.

# 49. STORY/CINEMATIC IDENTIFIERS

Use predictable IDs:
- `CIN_OPENING_001`
- `CIN_EARTH_001`
- `CIN_EARTH_WARP_001`
- `CIN_PYRAX_INTRO_001`
- `CIN_BOSS_MAGMA_INTRO_001`
- `CIN_BOSS_MAGMA_DEFEAT_001`
- `CIN_FINAL_REVEAL_001`

Mission IDs:
- `MISSION_EARTH_01`
- `MISSION_EARTH_02`

Wave IDs:
- `WAVE_EARTH_01_01`

Enemy IDs:
- `ENEMY_ZUPP_SCOUT`

Weapon IDs:
- `WEAPON_PLASMA`

# 50. TESTING REQUIREMENTS

Test every feature for:

**Functional correctness:** Does it work?

**Edge cases:** player death during effects, boss death during abilities, scene changes during active enemies, simultaneous pickup/death, skipped cinematics and app closure during progression.

**Performance:** frame drops, memory usage, garbage collection spikes.

**Mobile usability:** movement accuracy, readable UI, projectile visibility and danger readability.

# 51. FIRST RELEASE SCOPE

The first public release does not need all six worlds.

Recommended initial release candidate:
- Earth world
- Several missions
- Core enemy subset
- One polished boss
- Core progression
- Basic upgrades
- Opening cinematic
- Earth transition cinematic
- Strong UI
- Audio
- VFX
- Save system
- Monetization foundation

Additional worlds can be updates.

# 52. DEVELOPMENT PRIORITY

**P0 — MUST WORK**
- Movement
- Shooting
- Enemy combat
- Collision
- Health/shield
- Level flow
- Death
- Victory

**P1 — CORE FUN**
- Enemy variety
- Power-ups
- Score
- Combo
- Hazards
- Boss

**P2 — PROGRESSION**
- Missions
- Currency
- Upgrades
- Unlocks
- Save

**P3 — PRESENTATION**
- Final art
- VFX
- Audio
- UI polish
- Cinematics

**P4 — EXPANSION**
- Additional worlds
- More bosses
- More enemies
- More weapons
- Advanced story

# 53. IMMEDIATE PRODUCTION ORDER

1. Finish Zupp Scout: head, face, body, armor, arms, legs, backpack, weapon, colors, turnaround, separated parts and animation plan.
2. Create player ship.
3. Implement the Scout in Unity.
4. Build core combat prototype: player + Scout + movement + auto-fire + destruction.
5. Create Earth environment and hazards.
6. Build Earth missions through the data-driven level system.
7. Create Earth boss.
8. Add progression and save.
9. Produce opening and Earth transition cinematics with Google Flow.
10. Polish the Earth vertical slice.
11. Expand to additional worlds only after the vertical slice is proven.

# 54. DEFINITION OF DONE

A feature is complete only when:
- Code compiles
- No known blocking errors remain
- Feature works in Unity
- Relevant edge cases are tested
- Mobile behavior is acceptable
- Correct approved art is used
- UI communicates the feature
- Save/progression implications are handled
- Documentation is updated where necessary

# 55. MASTER DIRECTIVE TO GEMINI MCP

You are assisting with the development of STARFALL.

Before implementing any major feature:

1. Read this document.
2. Inspect the existing Unity project.
3. Identify reusable systems.
4. Explain the implementation plan briefly.
5. Make the smallest clean change that satisfies the requirement.
6. Test the change.
7. Report exactly what changed.
8. Identify anything that remains unverified.
9. Do not silently change the game design.
10. Keep the project modular, data-driven and mobile-friendly.

When creating levels, design the gameplay pacing first and implementation second.

When adding story, define the narrative event first, then create a modular cinematic shot plan, then create the individual Google Flow prompts, then connect the approved final video to the appropriate game event.

When using Google Flow, treat every generated clip as one shot in a larger edited cinematic, not as an entire movie.

When creating new characters, enemies, bosses or environments, preserve STARFALL's canonical visual identity and original-IP requirements.

The objective is a polished, original, fun 2D arcade space shooter that can grow from a small vertical slice into a full campaign without requiring a complete rewrite.

---

# 56. NEXT DESIGN TASK

The immediate design task is to finish the **Zupp Scout** as the first canonical production character. Once approved, use its visual language to design the next Zupp enemy rather than generating unrelated alien designs.

After the Scout and player ship are approved, begin the core Unity prototype.
