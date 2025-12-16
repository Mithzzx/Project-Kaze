# HiZ Occlusion Culling System

I have implemented a robust Hierarchical Z-Buffer (HiZ) occlusion culling system. This system is much more stable and performant than the previous single-point sampling method.

## How it works

1.  **HiZ Generation**: Every frame, the system takes the Camera's Depth Texture and creates a "mip chain" (downsampled versions).
    *   Each level contains the *farthest* depth value of the 2x2 block from the previous level.
    *   This allows us to query a large area of the screen with a single texture sample.
2.  **Culling**:
    *   The grass compute shader calculates the screen-space bounding box of each grass blade/clump.
    *   It selects the appropriate mip level where the grass covers roughly 1 pixel.
    *   It samples the HiZ texture.
    *   If the grass is farther than the HiZ value (which represents the farthest visible point in that area), it is definitely occluded.

## Setup Instructions

1.  **Assign Compute Shader**:
    *   Select your `GrassRenderer` GameObject.
    *   In the Inspector, find the **References** section.
    *   You will see a new field: **Hiz Compute Shader**.
    *   Assign the `HiZGenerator.compute` shader (located in `Assets/Shaders/`) to this field.

2.  **Enable Depth Texture**:
    *   Ensure **Depth Texture** is enabled in your URP Asset settings.

3.  **Verify**:
    *   Enable **Show Debug UI** in the Grass Renderer inspector.
    *   You should see "Depth Texture: Found".
    *   Toggle "Use Occlusion Culling" to see the effect.

## Troubleshooting

*   **Grass Disappears**: If all grass disappears, check if the `HiZGenerator` shader is assigned. If it's missing, the HiZ texture will be empty (black/white), causing incorrect culling.
*   **Flickering**: Increase **Occlusion Bias** slightly.
*   **Performance**: The HiZ generation pass is very fast (compute shader based), but it does add a small overhead. The performance gain from culling invisible grass should far outweigh this cost.
