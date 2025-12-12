using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

public class SkyboxSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class SkyboxProfile
    {
        public string profileName;
        public Material skyboxMaterial;
        public GameObject directionalLightPrefab;
        
        [Header("Lighting Settings")]
        [Range(0, 2)] public float ambientIntensity = 1.0f;
        [Range(0, 2)] public float reflectionIntensity = 1.0f;
        
        [Header("Fog Settings")]
        public bool fogEnabled = true;
        public Color fogColor = new Color(0.5f, 0.5f, 0.5f);
        [Range(0, 0.1f)] public float fogDensity = 0.01f;
    }

    [Header("Configuration")]
    public SkyboxProfile[] profiles;
    public bool switchOnStart = true;
    public int defaultProfileIndex = 0;

    [Header("Controls")]
    public Key nextKey = Key.RightArrow;
    public Key prevKey = Key.LeftArrow;
    
    private int currentProfileIndex = 0;
    private GameObject currentLightInstance;

    private void Start()
    {
        if (profiles.Length > 0 && switchOnStart)
        {
            currentProfileIndex = defaultProfileIndex;
            ApplyProfile(currentProfileIndex);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[nextKey].wasPressedThisFrame)
        {
            NextProfile();
        }
        else if (Keyboard.current[prevKey].wasPressedThisFrame)
        {
            PreviousProfile();
        }
    }

    public void NextProfile()
    {
        if (profiles.Length == 0) return;
        currentProfileIndex = (currentProfileIndex + 1) % profiles.Length;
        ApplyProfile(currentProfileIndex);
    }

    public void PreviousProfile()
    {
        if (profiles.Length == 0) return;
        currentProfileIndex--;
        if (currentProfileIndex < 0) currentProfileIndex = profiles.Length - 1;
        ApplyProfile(currentProfileIndex);
    }

    public void ApplyProfile(int index)
    {
        if (index < 0 || index >= profiles.Length) return;

        SkyboxProfile profile = profiles[index];

        // 1. Apply Skybox
        if (profile.skyboxMaterial != null)
        {
            RenderSettings.skybox = profile.skyboxMaterial;
        }

        // 2. Handle Directional Light
        // First, find any existing directional lights that might conflict if we didn't spawn them
        // (Optional: you might want to disable existing lights instead of destroying)
        
        if (currentLightInstance != null)
        {
            Destroy(currentLightInstance);
        }
        else
        {
            // If we haven't spawned one yet, maybe there's one in the scene we should disable?
            // For now, let's assume the user wants this script to manage the main light entirely.
            // You might want to manually delete the default Directional Light in your scene.
        }

        if (profile.directionalLightPrefab != null)
        {
            currentLightInstance = Instantiate(profile.directionalLightPrefab);
            currentLightInstance.name = $"Directional Light ({profile.profileName})";
            
            // Ensure it's set as the sun for skybox calculations
            Light lightComp = currentLightInstance.GetComponent<Light>();
            if (lightComp != null && lightComp.type == LightType.Directional)
            {
                RenderSettings.sun = lightComp;
            }
        }

        // 3. Apply Lighting Settings
        RenderSettings.ambientIntensity = profile.ambientIntensity;
        RenderSettings.reflectionIntensity = profile.reflectionIntensity;
        
        // 4. Apply Fog
        RenderSettings.fog = profile.fogEnabled;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.fogDensity = profile.fogDensity;

        // 5. Update Global Illumination
        // This is crucial for the ambient light to update based on the new skybox
        DynamicGI.UpdateEnvironment();
        
        Debug.Log($"Switched to Skybox Profile: {profile.profileName}");
    }
}
