# Grass Occlusion Culling System (Updated)

I have updated the Occlusion Culling system to be more robust and compatible with URP.

## Changes Made

1.  **Direct Texture Loading**: Switched from `SampleLevel` to `Load` in the compute shader. This avoids issues with missing samplers and provides more precise depth fetching.
2.  **Screen Parameters**: Added `_ScreenParams` to correctly map UV coordinates to pixel coordinates for the `Load` operation.
3.  **Safety Checks**: The system now gracefully handles cases where the depth texture might be missing (though it relies on you enabling it).

## Important: Enable Depth Texture

For this system to work, you **MUST** enable the Depth Texture in your URP Asset.

1.  Locate your **URP Asset** (usually in `Settings/` or `Rendering/`).
2.  In the Inspector, find the **General** section.
3.  Check **Depth Texture**.

If this is not enabled, the grass will either not be culled or might disappear entirely depending on the default value of the unbound texture.

## Troubleshooting

*   **Grass Disappears**: If all grass disappears when you enable "Use Occlusion Culling", it means the depth texture is reading as "0" (near plane) everywhere. Ensure Depth Texture is enabled in URP settings.
*   **Grass Flickering**: Increase the `Occlusion Bias` value in the Grass Renderer inspector (e.g., to 0.2 or 0.5).
*   **Performance**: This system uses the *previous frame's* depth buffer when running in `Update()`. This is standard for compute-shader based culling and is very performant, but fast camera movements might show a 1-frame lag in culling (usually unnoticeable for grass).
