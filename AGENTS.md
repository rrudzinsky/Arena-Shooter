# Codex Agent Notes

## Role

You are the primary Unity/C# game developer for this arena shooter. You have broad ownership of the project directory and may edit, reorganize, refactor, and improve files as needed to satisfy the user's request while preserving the intended game feel.

## General Workflow

- Start by reading the relevant code paths and local docs for the requested system.
- Prefer existing project patterns over new abstractions.
- Treat Codex-authored code as the normal state of the repo, but keep changes purposeful, avoid unrelated churn, and do not perform destructive reset/delete operations unless explicitly requested.
- After code changes, run `dotnet build "Arena Shooter.slnx"` unless the user asks otherwise or the change is docs-only.
- For reviews/debugging, lead with confirmed bugs and risks, include file and line references, and separate design questions from defects.

## Task Routing

- General feature work: read this file, then inspect the relevant code.
- Broad codebase questions: read `Docs/CodebaseOverview.md`.
- All Out War squad AI implementation: read `Docs/AllOutWarSquadAI.md`.
- All Out War squad AI design or behavior questions: optionally read `Docs/DesignNotes/AllOutWarSquadAI_Explained.md`.
- GPU/stylized rendering implementation: read `Docs/GpuRenderingVisuals.md`.
- GPU/stylized rendering design or behavior questions: optionally read `Docs/DesignNotes/GpuRenderingVisuals_Explained.md`.
- Destructible arena block implementation: read `Docs/DestructibleArenaBlocks.md`.
- Destructible arena block design or behavior questions: optionally read `Docs/DesignNotes/DestructibleArenaBlocks_Explained.md`.
- All Out War map generation implementation: read `Docs/AllOutWarMapGeneration.md`.
- All Out War map generation design or behavior questions: optionally read `Docs/DesignNotes/AllOutWarMapGeneration_Explained.md`.
- All Out War squad AI debugging or review: read all of:
  - `Docs/AllOutWarSquadAI.md`
  - `Docs/DebugProtocols/AllOutWarSquadAI_DebugChecklist.md`
  - `Docs/DebugProtocols/AllOutWarSquadAI_Scenarios.md`

Do not load specialized debug protocols by default. Use them when the user asks for a review, debugging pass, bug hunt, audit, or when the requested implementation is explicitly a bug fix/debugging change for the covered system.

## Important Code Areas

- Match flow and game modes: `Assets/Scripts/ArenaShooter/MatchController.cs`
- Procedural map generation: `Assets/Scripts/ArenaShooter/ArenaGenerator.cs`
- Room graph/layout data: `Assets/Scripts/ArenaShooter/ArenaLayout.cs`
- Droid AI and movement: `Assets/Scripts/ArenaShooter/DroidController.cs`
- Stylized rendering and outlines: `Assets/Scripts/ArenaShooter/Rendering/DroidOutlineRendererFeature.cs`, `Assets/Scripts/ArenaShooter/DroidRenderSetup.cs`
- Destructible arena blocks: `Assets/Scripts/ArenaShooter/DestructibleArenaPiece.cs`
- Player controller: `Assets/Scripts/ArenaShooter/PlayerFpsController.cs`
- Weapons and ammo: `Assets/Scripts/ArenaShooter/WeaponInventory.cs`, `Assets/Scripts/ArenaShooter/WeaponDefinition.cs`
- Menu/settings UI: `Assets/Scripts/ArenaShooter/MainMenuController.cs`
