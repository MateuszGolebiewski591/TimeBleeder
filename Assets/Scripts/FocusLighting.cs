using UnityEngine;
using System.Collections.Generic;

// Local-only lighting: the lights over whichever area your camera is closest to are lit,
// the others fade to almost nothing. Each player sees their own version, so the other
// person's board stays dark until you move the camera over to it.
public class FocusLighting : MonoBehaviour
{
    [System.Serializable]
    public class Area
    {
        [Tooltip("Where this area is, e.g. the board or the player's seat.")]
        public Transform centre;
        [Tooltip("Lights that should only be on while you're looking at this area.")]
        public Light[] lights;
    }

    [SerializeField] private Area[] areas;
    [Tooltip("How bright an unfocused area's lights are, as a fraction of their normal brightness.")]
    [Range(0f, 1f)] [SerializeField] private float dimmedFraction = 0.05f;
    [Tooltip("How quickly lights fade in/out (higher = faster).")]
    [SerializeField] private float fadeSpeed = 3f;

    // Each light's Inspector intensity is its "focused" brightness
    private readonly Dictionary<Light, float> baseIntensity = new Dictionary<Light, float>();
    private readonly HashSet<Light> focusedLights = new HashSet<Light>();
    private Camera localCamera;

    private void Start()
    {
        foreach (Area area in areas)
            foreach (Light light in area.lights)
                if (light != null && !baseIntensity.ContainsKey(light))
                    baseIntensity[light] = light.intensity;
    }

    private void LateUpdate()
    {
        Camera cam = GetLocalCamera();
        if (cam == null || areas == null || areas.Length == 0) return;

        // The area the camera is pointing most directly at is the one being looked at
        // (distance alone doesn't work when a camera spot sits between two boards)
        Area focused = null;
        float bestAlignment = float.MinValue;
        foreach (Area area in areas)
        {
            if (area.centre == null) continue;
            Vector3 toArea = (area.centre.position - cam.transform.position).normalized;
            float alignment = Vector3.Dot(cam.transform.forward, toArea); // 1 = straight ahead
            if (alignment > bestAlignment)
            {
                bestAlignment = alignment;
                focused = area;
            }
        }

        focusedLights.Clear();
        if (focused != null)
            foreach (Light light in focused.lights)
                if (light != null) focusedLights.Add(light);

        float blend = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime); // Frame-rate independent fade
        foreach (KeyValuePair<Light, float> entry in baseIntensity)
        {
            if (entry.Key == null) continue;
            float target = focusedLights.Contains(entry.Key) ? entry.Value : entry.Value * dimmedFraction;
            entry.Key.intensity = Mathf.Lerp(entry.Key.intensity, target, blend);
        }
    }

    // The local player's own camera. Camera.main isn't reliable here because the
    // scene camera is also tagged MainCamera.
    private Camera GetLocalCamera()
    {
        if (localCamera != null && localCamera.isActiveAndEnabled) return localCamera;

        foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!player.IsOwner) continue;
            localCamera = player.GetComponent<Camera>();
            if (localCamera != null) return localCamera;
        }
        return Camera.main; // Before the player has spawned
    }
}
