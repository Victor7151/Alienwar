# STARFALL --- Game Design Document

## Version 0.1 --- Foundation / Pre-Production

**Project type:** 2D mobile arcade space shooter\
**Primary platform:** Android\
**Development engine:** Unity\
**Cinematic tool:** Google Flow + video editing\
**Development assistant:** Gemini MCP\
**Current status:** Pre-production

------------------------------------------------------------------------

# 1. Project Vision

STARFALL is a fast, accessible 2D arcade space shooter inspired by the
nostalgic feeling of classic space-shooter games, while using an
entirely original world, creatures, characters, story, mechanics and
visual identity.

The player controls a human combat spacecraft during an interplanetary
invasion by bizarre, funny and dangerous alien creatures.

The experience combines:

-   Fast arcade combat
-   Enemy waves
-   Dodging and positioning
-   Power-ups
-   Weapons and upgrades
-   Environmental hazards
-   Warp events
-   Boss battles
-   Missions
-   Long-term progression
-   Humorous characters
-   A mystery-driven campaign

The game should be easy to understand within seconds but provide enough
depth to reward skilled players.

The intended emotional progression is:

**Curiosity → Fun → Action → Chaos → Discovery → Mystery → Cooperation →
Final confrontation**

------------------------------------------------------------------------

# 2. Core Player Fantasy

The player should feel like:

> "I am one of humanity's last space pilots, fighting ridiculous alien
> creatures and gradually discovering that the invasion is much bigger
> than it first appeared."

The game should feel:

-   Fast
-   Responsive
-   Satisfying
-   Funny
-   Cinematic
-   Colorful
-   Rewarding
-   Easy to pick up
-   Difficult to master

------------------------------------------------------------------------

# 3. Core Gameplay Loop

The fundamental loop is:

**Enter mission → Fight enemy waves → Dodge attacks and hazards →
Collect power-ups → Build combo → Survive → Defeat mini-boss/boss → Earn
rewards → Upgrade → Unlock next mission**

The game must prioritize immediate fun over complicated menus or
systems.

------------------------------------------------------------------------

# 4. Game Format

## Perspective

2D horizontal arcade shooter.

## Camera

A side-facing 2D gameplay camera with controlled scrolling/parallax
backgrounds.

## Controls

Primary control proposal:

-   Touch-and-drag movement
-   Automatic weapon firing
-   Touch-accessible special ability
-   Optional alternative control scheme if testing shows a virtual
    joystick performs better

The player should not need to repeatedly tap to fire during normal
gameplay.

## Session Length

Target normal mission duration:

**Approximately 3--7 minutes**, depending on mission design.

Boss missions may be longer.

------------------------------------------------------------------------

# 5. Story Overview

Humanity has searched the stars for evidence of intelligent
extraterrestrial life for generations.

Then astronomers detect thousands of objects approaching the solar
system.

The objects do not behave like asteroids.

They change direction.

They accelerate.

They are controlled.

The first invasion begins.

Human defense forces attempt to stop the attackers, but Earth is
overwhelmed.

The attackers appear to be bizarre alien creatures rather than
conventional machines.

However, scientists discover a disturbing pattern: the invaders are
targeting specific locations connected to a mysterious energy system
known as STARFALL.

Humanity believes the creatures want to conquer Earth.

Later, the player discovers that the invasion is connected to an ancient
network of interplanetary gateways.

The aliens are not simply invading.

They are searching for Starfall technology and attempting to escape an
even greater threat.

Eventually, humanity and the alien creatures may have to cooperate
against the real enemy.

------------------------------------------------------------------------

# 6. Story Structure

## Act I --- The Invasion

Humanity detects the approaching fleet.

The alien creatures attack Earth.

Earth's defenses begin collapsing.

The player launches the last experimental combat spacecraft.

## Act II --- The Chase

The player follows the invaders through space.

A Starfall-powered warp event transports the player beyond the solar
system.

The player discovers alien worlds and increasingly strange technology.

## Act III --- The Mystery

Evidence suggests the invasion is not what humanity believed.

Alien technology contains connections to Earth and the Starfall project.

The player learns that the creatures are running from something.

## Act IV --- The Alliance

Humanity and the alien creatures are forced into an uneasy alliance.

The player fights alongside former enemies.

## Act V --- The Void

The player reaches the source of the disturbance.

The true threat is revealed.

The final battle determines the fate of Earth and the worlds connected
by Starfall.

------------------------------------------------------------------------

# 7. Opening Cinematic

The opening story must be designed specifically for Google Flow.

Google Flow clips are treated as short modular shots, approximately
5--10 seconds each.

The opening will therefore be constructed as individual shots that can
be edited together.

Initial shot plan:

1.  Peaceful Earth from space.
2.  Earth's night side.
3.  Astronomers monitoring deep-space signals.
4.  Strange objects appear on a radar display.
5.  Objects change direction toward Earth.
6.  Scientists realize the objects are controlled.
7.  Governments begin emergency preparations.
8.  First strange alien craft enters Earth's atmosphere.
9.  More craft appear.
10. First funny alien creature is revealed.
11. Alien fleet approaches Earth.
12. Human defense fleet launches.
13. First large-scale battle.
14. Human fleet begins losing.
15. Starfall facility activates.
16. Alien technology reacts to Starfall.
17. The player's experimental spacecraft is revealed.
18. Player ship launches.
19. Player enters the battle.
20. Title reveal.
21. Gameplay begins.

This list is a planning structure, not a final Flow prompt.

------------------------------------------------------------------------

# 8. Cinematic Production Rules

All story cinematics must be designed as modular short shots.

Each shot should specify:

-   Characters
-   Creature appearance
-   Ship appearance
-   Environment
-   Lighting
-   Camera angle
-   Camera movement
-   Action
-   Mood
-   Approximate duration
-   Beginning state
-   Ending state
-   Continuity requirements

Important continuity rule:

A generated clip must end in a state that makes the next clip easy to
connect to it.

The cinematic bible will maintain canonical descriptions of recurring
characters, creatures, ships and environments.

------------------------------------------------------------------------

# 9. Alien Species

## Working name

**Zupps**

This name is temporary and may be replaced.

## Species identity

The Zupps are funny, expressive, chaotic extraterrestrial creatures with
advanced technology.

They are not intended to be generic "little green aliens."

They should have:

-   Strong silhouettes
-   Large expressive faces
-   Exaggerated reactions
-   Distinct body shapes
-   Memorable visual features
-   Colorful designs
-   Funny behavior
-   Advanced but slightly ridiculous technology

## Personality

Zupps are generally:

-   Curious
-   Chaotic
-   Competitive
-   Expressive
-   Easily distracted
-   Occasionally brilliant
-   Occasionally very foolish

They are not inherently evil.

Their motivations become clearer as the story progresses.

------------------------------------------------------------------------

# 10. Alien Humor

Humor should come from behavior, animation and situations rather than
constant jokes.

Possible examples:

-   A pilot panics when its weapon malfunctions.
-   Two enemy ships accidentally collide.
-   A creature struggles with an oversized weapon.
-   A Zupp celebrates too early and gets hit.
-   A tiny creature commands an enormous machine.
-   An enemy repairs its ship while still trying to attack.

Humor must not make combat feel harmless.

The enemies can be funny and genuinely dangerous at the same time.

------------------------------------------------------------------------

# 11. Main Alien Character

Working name:

**General Blob**

This is temporary.

General Blob is a high-ranking Zupp commander.

At first he appears to be a major antagonist.

Later the player discovers that he understands the Starfall threat and
has been trying to reach Earth because the Zupps are fleeing a greater
danger.

The character should combine:

-   Comedy
-   Intelligence
-   Authority
-   Mystery
-   Emotional depth

------------------------------------------------------------------------

# 12. Enemy Roster

Initial enemy classes:

## Scout

-   Fast
-   Low health
-   Simple movement
-   Introduces basic combat pressure

## Shooter

-   Maintains distance
-   Fires projectiles
-   Forces the player to dodge

## Bomber

-   Slow
-   Drops explosives
-   Controls space

## Charger

-   Locks onto the player
-   Rushes toward the ship
-   Requires quick reactions

## Shielder

-   Protects nearby enemies
-   Encourages target prioritization

## Tank

-   High health
-   Slow
-   Heavy attacks

## Teleporter

-   Disappears and reappears
-   Creates unpredictable movement

## Swarm

-   Many small enemies
-   Rewards rapid shooting and multi-kills

## Elite

-   Stronger variant
-   Better rewards
-   Distinct behavior or weapon

Additional enemy types will be added as worlds are designed.

------------------------------------------------------------------------

# 13. Player Ship

The player's spacecraft is an experimental human combat ship created
through Project Starfall.

It should have:

-   Strong recognizable silhouette
-   Clear cockpit
-   Main weapon emitters
-   Shield generator
-   Engine/boost effect
-   Upgradeable systems
-   Special ability visual effects

The final ship design is TBD.

------------------------------------------------------------------------

# 14. Combat

Combat should be immediately understandable.

Core actions:

-   Move
-   Auto-fire
-   Dodge
-   Collect
-   Activate special ability

Combat should reward positioning rather than simply holding one
position.

Enemy attacks should create readable patterns.

The player should be able to understand why they were hit.

------------------------------------------------------------------------

# 15. Weapons

Initial weapon categories:

## Plasma Shot

Basic reliable weapon.

## Triple Shot

Fires three projectiles.

## Flame Burst

Powerful short-range attack.

## Laser Beam

Continuous or sustained damage.

## Missile Storm

Tracking projectiles.

## Star Cannon

High-powered temporary weapon.

Exact damage, fire rates and progression will be balanced during
prototyping.

------------------------------------------------------------------------

# 16. Weapon Progression

Weapons can have levels.

Example:

Level 1 → Level 2 → Level 3 → Level 4 → Level 5

Higher levels may improve:

-   Damage
-   Projectile count
-   Fire rate
-   Projectile size
-   Special effects
-   Critical chance

Exact progression is TBD.

------------------------------------------------------------------------

# 17. Health and Shield

The ship has two defensive layers:

## Shield

Absorbs incoming damage.

May recharge after a period without taking damage.

## Hull

Permanent mission health.

If hull reaches zero, the player dies unless a revive is used.

This system creates a risk/reward decision: aggressive play is possible,
but avoiding damage is valuable.

------------------------------------------------------------------------

# 18. Special Ability

The player has a rechargeable special/ultimate ability.

Possible behavior:

-   Charge through combat
-   Activate when needed
-   Deliver a powerful attack or defensive effect
-   Use dramatic VFX
-   Have a cooldown or energy requirement

The final ability is TBD.

------------------------------------------------------------------------

# 19. Power-Ups

Initial power-up categories:

-   Weapon upgrade
-   Shield restore
-   Hull repair
-   Fire-rate boost
-   Temporary overdrive
-   Bomb/AOE attack
-   Score multiplier
-   Special-energy recharge

Power-ups should be visually obvious during gameplay.

------------------------------------------------------------------------

# 20. Score and Combo

Destroying enemies produces score.

Continuous successful play builds a combo.

Example:

x2 → x3 → x4 → x5...

Taking damage may reduce or reset the combo depending on final
balancing.

High combos should produce:

-   Higher scores
-   More satisfying feedback
-   Potential reward bonuses

------------------------------------------------------------------------

# 21. Currency

The game should have an earnable in-game currency.

Working name:

**Star Credits**

This is temporary.

Currency can be earned through:

-   Completing missions
-   Defeating bosses
-   Missions/challenges
-   High scores
-   Daily rewards
-   Optional rewarded ads

Currency can be spent on ship upgrades and other progression systems.

------------------------------------------------------------------------

# 22. Ship Upgrades

Potential upgrade categories:

-   Weapon damage
-   Fire rate
-   Shield capacity
-   Hull health
-   Movement speed
-   Energy capacity
-   Special ability
-   Critical chance

Upgrades must be meaningful without making early content trivial.

------------------------------------------------------------------------

# 23. Missions

Mission objectives can include:

-   Destroy a specific number of enemies
-   Reach a combo threshold
-   Finish without losing shield
-   Defeat a boss
-   Collect power-ups
-   Use a specific weapon
-   Finish without reviving
-   Survive for a specific time

Mission rewards may include currency, upgrade materials or unlocks.

------------------------------------------------------------------------

# 24. Worlds

Initial campaign structure:

## World 1 --- Earth

Theme:

-   Earth
-   Atmosphere
-   Orbit
-   Moon
-   Satellites
-   Space debris

Hazards:

-   Asteroids
-   Debris
-   Missiles
-   Energy fields

Purpose:

Teach the player the core mechanics.

------------------------------------------------------------------------

## World 2 --- Pyrax

Volcanic alien world.

Visuals:

-   Lava
-   Volcanoes
-   Smoke
-   Glowing rocks
-   Volcanic structures

Hazards:

-   Lava bursts
-   Falling rocks
-   Fireballs
-   Volcanic explosions

------------------------------------------------------------------------

## World 3 --- Frostara

Frozen alien world.

Visuals:

-   Ice
-   Snow
-   Ice mountains
-   Aurora
-   Crystals

Hazards:

-   Ice fragments
-   Ice storms
-   Freezing fields
-   Large ice formations

------------------------------------------------------------------------

## World 4 --- Nexus

Artificial technological world.

Visuals:

-   Giant machinery
-   Energy towers
-   Futuristic structures
-   Portals
-   Mechanical cities

Hazards:

-   Laser barriers
-   Energy walls
-   Gravity fields
-   Teleportation systems

------------------------------------------------------------------------

## World 5 --- Outer Rift

Unstable region of space.

Visuals:

-   Nebulas
-   Wormholes
-   Space fractures
-   Distorted stars
-   Floating debris

Gameplay focus:

-   Warp events
-   Teleporting enemies
-   Unpredictable environmental changes

------------------------------------------------------------------------

## World 6 --- The Void

Final region.

Visuals:

-   Dark space
-   Massive structures
-   Strange energy
-   Reality distortions

Purpose:

Final story reveal and confrontation.

The exact number of worlds may change during production.

------------------------------------------------------------------------

# 25. Level Structure

A typical mission may contain:

**Intro → Wave 1 → Wave 2 → Hazard introduction → Wave 3 → Power-up
opportunity → Elite/miniboss → Warp/event → Final wave → Boss or
extraction → Rewards**

Not every mission must contain every element.

Variation is important.

------------------------------------------------------------------------

# 26. Warp System

Warp events are a signature feature.

Possible sequence:

1.  Warning appears.
2.  Screen distortion begins.
3.  Sound/music changes.
4.  Space bends toward a portal.
5.  Player is transported.
6.  New environment appears.
7.  Gameplay resumes.

Warp events can be used to:

-   Transition between worlds
-   Introduce surprise encounters
-   Change enemy formations
-   Change hazards
-   Create special challenge stages

------------------------------------------------------------------------

# 27. Boss Design

Bosses should have multiple phases rather than one large health bar.

Possible structure:

**Phase 1:** Basic attacks\
**Phase 2:** New attack pattern\
**Phase 3:** Summons enemies\
**Phase 4:** Armor/core exposed\
**Final Phase:** Powerful signature attack

Bosses should have:

-   Strong visual identity
-   Telegraphs for dangerous attacks
-   Distinct patterns
-   Destructible components where useful
-   Satisfying defeat animation
-   Unique rewards

------------------------------------------------------------------------

# 28. Initial Boss Roadmap

## Earth

**The Invader Carrier**

A large alien carrier that introduces boss mechanics.

## Pyrax

**The Magma Machine**

Heavy war machine adapted to volcanic conditions.

## Frostara

**The Frost Warden**

Ice-based boss with freezing attacks.

## Nexus

**The Core Guardian**

Technological boss using lasers, portals and energy fields.

## Outer Rift

**Rift Beast / TBD**

A boss built around unstable space.

## The Void

**Final Threat / TBD**

The true enemy behind the events.

Names are provisional.

------------------------------------------------------------------------

# 29. Player Progression

The campaign progression is:

**Mission completion → Rewards → Ship upgrades → New missions → New
worlds → New enemies → New mechanics**

The game should always give the player a clear reason to continue.

------------------------------------------------------------------------

# 30. UI/UX

Core screens:

-   Splash/loading
-   Main menu
-   Story/cinematic
-   World selection
-   Mission selection
-   Gameplay HUD
-   Pause
-   Victory
-   Defeat
-   Rewards
-   Hangar
-   Ship upgrades
-   Weapon upgrades
-   Missions/challenges
-   Settings

Gameplay HUD should clearly display:

-   Health
-   Shield
-   Current weapon
-   Special ability
-   Score
-   Combo
-   Relevant mission objective

UI should remain readable on mobile screens.

------------------------------------------------------------------------

# 31. Art Direction

Overall style:

**Premium stylized 2D sci-fi arcade art.**

Avoid:

-   Generic stock-looking sci-fi art
-   Realistic military simulation
-   Pixel art unless deliberately used for a special feature
-   Childish nursery aesthetics
-   Excessively complicated UI

Prioritize:

-   Strong silhouettes
-   Expressive characters
-   Vibrant environments
-   Clear projectiles
-   Large satisfying explosions
-   Layered parallax backgrounds
-   Strong VFX
-   Readable enemy types

------------------------------------------------------------------------

# 32. Animation

Important animation categories:

-   Player idle/thrust
-   Player movement tilt
-   Shooting
-   Damage
-   Shield hit
-   Death
-   Enemy movement
-   Enemy attack
-   Enemy hit
-   Enemy death
-   Power-up floating
-   Power-up collection
-   Boss phases
-   Warp effects

Animation can use Unity 2D animation, sprite animation, shaders,
particle effects and Rive where appropriate.

------------------------------------------------------------------------

# 33. VFX

VFX should be a major part of the game's feel.

Examples:

-   Muzzle flashes
-   Lasers
-   Plasma trails
-   Explosions
-   Enemy destruction
-   Shield impact
-   Shield recharge
-   Power-up glow
-   Hit sparks
-   Screen shake
-   Warp distortion
-   Energy pulses
-   Boss damage
-   Environmental effects

VFX must remain performant on mobile.

------------------------------------------------------------------------

# 34. Audio

Audio categories:

-   Weapon firing
-   Enemy firing
-   Enemy hit
-   Enemy destruction
-   Power-up pickup
-   Shield impact
-   Damage warning
-   Boss warning
-   Warp
-   Explosion
-   UI interaction
-   Mission completion

Music should support:

-   Exploration
-   Combat
-   Boss battles
-   Story cinematics
-   Victory
-   Defeat

The Zupps may have recognizable comedic sound cues.

------------------------------------------------------------------------

# 35. Mobile Performance

Performance is a core requirement.

Priorities:

-   Object pooling
-   Efficient particle systems
-   Sprite atlases
-   Avoid unnecessary Instantiate/Destroy calls during combat
-   Controlled enemy counts
-   Efficient collision handling
-   Avoid expensive per-frame searches
-   Minimize garbage generation
-   Optimize textures
-   Use appropriate compression
-   Test on lower-end Android devices
-   Maintain stable frame rate

Exact performance target will be established during prototype testing.

------------------------------------------------------------------------

# 36. Unity Architecture

The project should be modular.

Likely systems:

-   GameManager
-   LevelManager
-   PlayerController
-   WeaponSystem
-   ProjectileSystem
-   EnemyController
-   EnemySpawner
-   WaveManager
-   BossController
-   HealthSystem
-   ShieldSystem
-   PowerUpSystem
-   ScoreSystem
-   ComboSystem
-   CurrencySystem
-   UpgradeSystem
-   MissionSystem
-   SaveSystem
-   AudioManager
-   VFXManager
-   UIManager
-   PoolManager
-   CinematicManager

These are proposed names, not final implementation requirements.

Gemini must inspect existing code before creating duplicate systems.

------------------------------------------------------------------------

# 37. Data Architecture

Where appropriate, game data should use ScriptableObjects or another
clean data-driven approach.

Potential data assets:

-   EnemyData
-   WeaponData
-   BossData
-   PowerUpData
-   ShipData
-   LevelData
-   WorldData
-   MissionData
-   UpgradeData

Hard-coded gameplay values should be minimized when designers may need
to tune them.

------------------------------------------------------------------------

# 38. Project Folder Structure

Proposed structure:

Assets/ - Art/ - Characters/ - Enemies/ - Player/ - Ships/ - Worlds/ -
Backgrounds/ - UI/ - VFX/ - Audio/ - Music/ - SFX/ - Animations/ -
Prefabs/ - Player/ - Enemies/ - Projectiles/ - PowerUps/ - Bosses/ -
UI/ - Scenes/ - Boot/ - Menus/ - Worlds/ - Scripts/ - Core/ - Player/ -
Combat/ - Enemies/ - Bosses/ - Levels/ - Progression/ - UI/ - Audio/ -
Save/ - ScriptableObjects/ - Resources/ - Documentation/

The actual structure may be adjusted to match Unity best practices and
the project's final architecture.

------------------------------------------------------------------------

# 39. Development Philosophy

Do not build the complete game immediately.

Development must proceed through playable prototypes.

## Prototype 1 --- Core Combat

Build:

-   Player ship
-   Touch movement
-   Auto-fire
-   One projectile
-   One enemy
-   Enemy spawning
-   Enemy destruction
-   Player damage/death

Goal:

**The game must already feel fun before adding progression.**

## Prototype 2 --- Combat Depth

Add:

-   Enemy formations
-   Asteroids
-   Health
-   Shield
-   Power-ups
-   Multiple enemy behaviors

## Prototype 3 --- Boss

Add:

-   Boss
-   Multiple boss phases
-   Boss attacks
-   Boss health
-   Boss defeat
-   VFX and audio

## Prototype 4 --- Progression

Add:

-   Missions
-   Currency
-   Upgrades
-   Rewards
-   Save system
-   Level progression

## Prototype 5 --- First Complete World

Build Earth as a polished vertical slice.

## Prototype 6 --- Content Expansion

Build additional worlds and enemies.

------------------------------------------------------------------------

# 40. Vertical Slice Goal

Before producing the full campaign, the project should have one polished
Earth-world slice containing:

-   Finished player controls
-   At least several enemy types
-   Weapons
-   Power-ups
-   Hazards
-   A mini-boss
-   A boss
-   Score/combo
-   Mission objective
-   Rewards
-   Basic upgrade
-   Cinematic transition
-   Audio
-   VFX
-   Mobile UI

This vertical slice becomes the quality benchmark for the rest of the
game.

------------------------------------------------------------------------

# 41. Monetization

Preferred philosophy:

**Free-to-play with player-friendly monetization.**

Potential monetization:

-   Optional rewarded ads
-   Optional revive
-   Optional bonus rewards
-   Cosmetic ship elements
-   Optional premium content
-   Future purchases if appropriate

Avoid excessive forced advertisements.

Monetization must never destroy the arcade flow.

------------------------------------------------------------------------

# 42. Google Flow Story Production

Google Flow will be used primarily for story/cinematic content.

Because generated clips are short, the production pipeline is:

**Story beat → Shot list → Individual 5--10 second prompts → Generate
clips → Select consistent takes → Edit together → Add
sound/music/dialogue/text → Final cinematic**

Every recurring visual element needs a canonical reference.

For example:

-   Player ship reference
-   General Blob reference
-   Zupp base design
-   Major boss references
-   Earth visual reference
-   Each planet visual reference

------------------------------------------------------------------------

# 43. Cinematic Continuity Rules

Every Flow prompt must preserve:

-   Same character design
-   Same ship design
-   Same clothing/accessories where applicable
-   Same environment
-   Consistent lighting
-   Consistent art direction
-   Consistent scale
-   Consistent camera language

When a new shot changes location or time, the prompt must explicitly
establish the transition.

Generated clips should not introduce random new characters or visual
elements unless intentionally requested.

------------------------------------------------------------------------

# 44. Marketing Direction

Potential promotional material:

-   Opening invasion trailer
-   Gameplay trailer
-   Boss reveal clips
-   Funny Zupp moments
-   Ship upgrade videos
-   Short social-media clips
-   Development/build-in-public posts
-   Google Play screenshots
-   Game icon
-   Feature graphics

Marketing should emphasize:

**Funny alien invasion + fast arcade space combat + mysterious story.**

------------------------------------------------------------------------

# 45. Build-in-Public

Development content can show:

-   Prototype progress
-   Enemy creation
-   Boss development
-   VFX
-   Google Flow cinematic experiments
-   Unity development
-   Before/after improvements
-   New worlds
-   Playtesting

The project can be documented publicly as part of the development
journey.

------------------------------------------------------------------------

# 46. Current Decisions

Locked or strongly preferred:

-   2D game
-   Mobile-first
-   Android first
-   Horizontal arcade shooter
-   Funny alien creatures
-   Original IP
-   Auto-fire as the starting control design
-   Touch-based movement
-   Multiple worlds
-   Warp events
-   Power-ups
-   Ship upgrades
-   Boss battles
-   Story-driven progression
-   Google Flow for cinematics
-   Short modular cinematic shots
-   Gemini MCP as development assistant
-   Player-friendly monetization

------------------------------------------------------------------------

# 47. Decisions Still Requiring Finalization

These should be decided before production begins:

-   Final game name
-   Final alien species name
-   Final alien visual design
-   Player identity/name
-   Final player ship design
-   Exact weapon roster
-   Exact special ability
-   Exact number of missions per world
-   Exact number of worlds in first release
-   Final boss designs
-   Final story ending
-   Final currency name
-   Final art palette
-   Final UI style
-   Final monetization details
-   Final audio direction
-   Final technical architecture

------------------------------------------------------------------------

# 48. Gemini MCP Development Rules

Gemini is an AI development assistant for this project.

## Rule 1 --- Source of truth

Treat this GDD and the project's technical documentation as the source
of truth.

## Rule 2 --- Inspect before modifying

Before creating or modifying code:

1.  Inspect the relevant existing files.
2.  Understand current architecture.
3.  Reuse existing systems where appropriate.
4.  Avoid creating duplicate managers or systems.

## Rule 3 --- Do not invent requirements

Do not introduce major gameplay features that conflict with the GDD.

If a new feature is necessary, identify it as a proposed change rather
than silently changing the design.

## Rule 4 --- Keep systems modular

Avoid giant monolithic scripts.

Use focused components and data-driven systems where appropriate.

## Rule 5 --- Mobile first

All gameplay systems must consider Android performance.

## Rule 6 --- Performance

Prefer pooling and efficient update patterns.

Avoid unnecessary runtime allocations and repeated expensive searches.

## Rule 7 --- Readability

Code should be understandable and maintainable.

Use clear naming.

Avoid unnecessary abstraction.

## Rule 8 --- Test incrementally

Do not implement large interconnected systems without testing the
smaller pieces.

## Rule 9 --- Preserve working functionality

Do not rewrite functioning systems unnecessarily.

When changing an existing system, identify potential side effects.

## Rule 10 --- Documentation

When a major system is created or changed, update the appropriate
project documentation.

------------------------------------------------------------------------

# 49. Production Milestones

### Milestone 1

Game concept locked.

### Milestone 2

GDD and technical documentation completed.

### Milestone 3

Core movement and shooting prototype.

### Milestone 4

Enemy/wave prototype.

### Milestone 5

Combat vertical slice.

### Milestone 6

First boss.

### Milestone 7

Earth world vertical slice.

### Milestone 8

Opening cinematic.

### Milestone 9

Progression and upgrades.

### Milestone 10

First release candidate.

------------------------------------------------------------------------

# 50. Current Immediate Next Step

Do not begin the full game yet.

The immediate next production task is:

## Create the visual identity package

1.  Finalize the game name direction.
2.  Design the Zupp species.
3.  Design the player ship.
4.  Establish the art style.
5.  Establish the visual references.
6.  Create canonical image references.
7.  Then finalize the cinematic bible.

After that:

**Technical architecture → Unity prototype → playable vertical slice.**

------------------------------------------------------------------------

# 51. Important IP Rule

This project may be inspired by the nostalgia and broad gameplay
category of classic arcade space shooters, but it must remain an
original intellectual property.

Do not copy:

-   Chicken Invaders characters
-   Chicken Invaders names
-   Chicken Invaders story
-   Chicken Invaders dialogue
-   Chicken Invaders artwork
-   Chicken Invaders levels
-   Chicken Invaders exact enemy designs
-   Chicken Invaders exact UI
-   Chicken Invaders exact sounds/music
-   Other protected game assets

Use the inspiration only at the level of broad genre concepts such as:

-   Space shooting
-   Enemy waves
-   Asteroids/hazards
-   Power-ups
-   Bosses
-   Arcade progression

Everything specific to STARFALL must be original.

------------------------------------------------------------------------

# 52. Design North Star

Whenever a design decision is uncertain, ask:

> **Does this make the game more fun to play, more recognizable, more
> original, or more memorable?**

If it does none of these, it probably does not need to be added.

The game should remain focused on:

**MOVE → SHOOT → DODGE → COLLECT → SURVIVE → DISCOVER → UPGRADE →
REPEAT**
