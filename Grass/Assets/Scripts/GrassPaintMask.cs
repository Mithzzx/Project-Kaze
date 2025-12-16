using UnityEngine;

/// <summary>
/// Stores the grass paint mask texture for defining where grass should grow.
/// This can be saved as an asset and shared between scenes.
/// </summary>
[CreateAssetMenu(fileName = "GrassMask", menuName = "Grass/Paint Mask", order = 1)]
public class GrassPaintMask : ScriptableObject
{
    [SerializeField] private int resolution = 512;
    [SerializeField] private Texture2D maskTexture;
    
    public int Resolution => resolution;
    public Texture2D MaskTexture => maskTexture;
    
    /// <summary>
    /// Creates a new mask texture with the specified resolution.
    /// </summary>
    public void CreateMask(int newResolution)
    {
        resolution = newResolution;
        maskTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false);
        maskTexture.filterMode = FilterMode.Bilinear;
        maskTexture.wrapMode = TextureWrapMode.Clamp;
        
        // Initialize to white (grass everywhere)
        Color[] pixels = new Color[resolution * resolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        maskTexture.SetPixels(pixels);
        maskTexture.Apply();
    }
    
    /// <summary>
    /// Clears the mask to a uniform value (0 = no grass, 1 = full grass)
    /// </summary>
    public void ClearMask(float value = 1f)
    {
        if (maskTexture == null)
        {
            CreateMask(resolution);
            return;
        }
        
        Color[] pixels = new Color[resolution * resolution];
        Color clearColor = new Color(value, value, value, 1f);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clearColor;
        }
        maskTexture.SetPixels(pixels);
        maskTexture.Apply();
    }
    
    /// <summary>
    /// Paints a circular area on the mask.
    /// </summary>
    /// <param name="normalizedCenter">Center position in 0-1 UV space</param>
    /// <param name="normalizedRadius">Radius in 0-1 UV space</param>
    /// <param name="intensity">Paint intensity (0 = remove grass, 1 = add grass)</param>
    /// <param name="hardness">Brush hardness (0 = soft falloff, 1 = hard edge)</param>
    public void Paint(Vector2 normalizedCenter, float normalizedRadius, float intensity, float hardness = 0.5f)
    {
        if (maskTexture == null)
        {
            CreateMask(resolution);
        }
        
        int centerX = Mathf.RoundToInt(normalizedCenter.x * resolution);
        int centerY = Mathf.RoundToInt(normalizedCenter.y * resolution);
        int radiusPixels = Mathf.RoundToInt(normalizedRadius * resolution);
        
        int minX = Mathf.Max(0, centerX - radiusPixels);
        int maxX = Mathf.Min(resolution - 1, centerX + radiusPixels);
        int minY = Mathf.Max(0, centerY - radiusPixels);
        int maxY = Mathf.Min(resolution - 1, centerY + radiusPixels);
        
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist <= radiusPixels)
                {
                    // Calculate falloff based on hardness
                    float normalizedDist = dist / radiusPixels;
                    float falloff = 1f - Mathf.Pow(normalizedDist, 1f / Mathf.Max(0.01f, 1f - hardness));
                    falloff = Mathf.Clamp01(falloff);
                    
                    Color currentColor = maskTexture.GetPixel(x, y);
                    float currentValue = currentColor.r;
                    
                    // Blend based on intensity
                    float newValue = Mathf.Lerp(currentValue, intensity, falloff);
                    maskTexture.SetPixel(x, y, new Color(newValue, newValue, newValue, 1f));
                }
            }
        }
        
        maskTexture.Apply();
    }
    
    /// <summary>
    /// Gets the grass density at a normalized position.
    /// </summary>
    public float GetDensityAt(Vector2 normalizedPos)
    {
        if (maskTexture == null) return 1f;
        
        int x = Mathf.Clamp(Mathf.RoundToInt(normalizedPos.x * resolution), 0, resolution - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(normalizedPos.y * resolution), 0, resolution - 1);
        
        return maskTexture.GetPixel(x, y).r;
    }
    
    private void OnValidate()
    {
        resolution = Mathf.Clamp(resolution, 64, 4096);
    }
}
