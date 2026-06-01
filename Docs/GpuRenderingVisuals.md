# GPU Rendering Visuals

This file is the compact implementation contract and invariant source of truth for the game's stylized GPU rendering and visual-outline system.

## Purpose

Arena Shooter uses a neon, high-contrast visual language. Gameplay objects are assigned stylized outline categories, then a URP renderer feature builds per-category masks and composites colored outlines back over the scene.

The goal is readable combat with clean neon silhouettes: walls, floors, droids, guns, ammo, and medical resources should remain visually distinct even in dense All Out War fights without turning into fuzzy over-bright bloom.

## Key Code Areas

- `Assets/Scripts/ArenaShooter/Rendering/DroidOutlineRendererFeature.cs` owns the URP outline mask and composite passes. The class keeps its legacy name for renderer-asset compatibility, but it is the general stylized outline feature.
- `Assets/Scripts/ArenaShooter/DroidRenderSetup.cs` assigns rendering layer masks, outline categories, colors, and shadow/probe settings.
- `Assets/Scripts/ArenaShooter/ArenaTheme.cs` creates shared matte, neon line, beam, and generated-asset materials.
- `Assets/Shaders/DroidOutlineMask.shader` writes outline mask data.
- `Assets/Shaders/DroidOutlineComposite.shader` detects mask edges and adds glow color to the scene.
- `Assets/Shaders/InvisibleOutlineProxy.shader` supports invisible outline sources for destructible wall bodies.
- `Assets/Scripts/ArenaShooter/ShieldDomeBackdrop.cs` builds the unlit shield dome backdrop.

## Outline Categories

`StylizedOutlineCategory` currently defines:

- `None`
- `Floor`
- `Wall`
- `Droid`
- `Medical`
- `Ammo`
- `Gun`

Each category maps to a rendering layer mask and outline color in `DroidRenderSetup`. The renderer feature's default bands must stay aligned with those masks.

Each outline band also has a presentation profile: hard-edge thickness, glow radius, glow strength, intensity, normal-edge contribution, and distance fade. This lets wall silhouettes stay thin and clean while droids, pickups, and guns remain readable.

## Invariants

- Renderers that should receive outlines must have their `renderingLayerMask` assigned through `DroidRenderSetup`.
- The Unity object layer named `DroidOutline` is only a layer fallback/context marker. Category selection is driven by rendering layer masks.
- The outline feature should run only for base cameras.
- Mask passes render opaque geometry for each enabled outline band.
- Composite passes should preserve the scene color and add outline glow, not replace the whole frame except in diagnostic modes.
- Outline presentation should use a crisp neon core plus controlled halo. Avoid returning to one global hot-blue fuzzy bloom for every category.
- Wall and pillar screen-space outlines should tune silhouette/alpha edges separately from normal/internal side lines. Top silhouettes should stay restrained while side lines can keep the brighter cyan reference style.
- Shared neon materials should use the same cleaner cyan/magenta/violet language as the outline pass instead of very high-intensity hot-pink bloom.
- Distance fading should keep far outlines readable without letting distant glow dominate the screen.
- Destructible walls that need wall outlines should use invisible outline proxy geometry instead of relying only on the changing damaged body mesh.
- Destructible wall proxies define the surviving silhouette shape. Shot/shatter damage contours use the shared wall-line color and should own torn edges where missing shot chunks border surviving material.
- Wall/pillar base accent geometry is visual-only. It should not add collision, destructible state, NavMesh geometry, or gameplay meaning.
- The shield dome is visual backdrop geometry. Physical All Out War boundary blocking belongs to the arena boundary ring, not dome mesh collision.

## Diagnostics

Outline diagnostics are intentionally verbose when playing. They report shader discovery, pass enqueue decisions, renderer category counts, per-band visibility, mask execution, composite execution, and optional GPU readbacks.

Use diagnostics when outlines disappear, show the wrong color, vanish by distance, or fail for a specific category.

## Known Boundaries

- This system is URP-specific.
- It is not a general post-processing stack.
- It does not decide gameplay visibility, target selection, projectile collision, or AI sight.
- The dome visuals are generated mesh bands and seams, not a physics shell.

## Related Docs

- `Docs/DesignNotes/GpuRenderingVisuals_Explained.md` is the narrative explanation for human/design understanding.
