# Painting and Editor Tools

## Goal
Artist-friendly control of grass distribution with immediate GPU feedback.

## Workflow
1) In the Inspector for GrassRenderer, click Start Painting.
2) If no density map exists, the tool creates a 512x512 R8 texture.
3) Paint in Scene view: Left Click to paint, Shift + Left Click to erase.
4) Adjust brush: size, opacity, hardness, target density, overlay toggle.
5) Save Mask to persist as PNG; texture re-imports as readable and uncompressed.

## Overlay and Gizmos
- Heatmap overlay: red tint; zero-density pixels discarded to keep terrain visible.
- Mini-map: 2D preview in Scene view for full-texture context.
- Brush gizmos: outer ring (size), inner ring (hardness), solid center.

## Controls and Properties
- densityMap: assigned texture; recommend R8, Read/Write enabled.
- densityThreshold: cutoff used during generation and culling.
- renderInEditMode: keep on to see live painting without Play mode.

## Best Practices
- Paint broad strokes at low opacity, refine with smaller brushes.
- Keep densityThreshold low (around 0.1) to allow smooth gradients.
- Use Clear Map for new biomes; Save Mask before large edits to version your work.
- Ensure a Terrain is assigned; UVs derive from terrain position and size.

## File References
- Tooling: [Assets/Scripts/Editor/GrassPainterEditor.cs](Assets/Scripts/Editor/GrassPainterEditor.cs)
- Runtime consumer: [Assets/Scripts/GrassRenderer.cs](Assets/Scripts/GrassRenderer.cs) (density sampling in CSMain/CSCull)
- Overlay shader: [Assets/Shaders/GrassDensityOverlay.shader](Assets/Shaders/GrassDensityOverlay.shader)
