using System;
using UnityEngine;

namespace Societies.Runtime.Simulation
{
    /// <summary>
    /// Drives a simple 24-hour day/night loop and updates ambient lighting.
    /// </summary>
    public class TimeSystem : MonoBehaviour
    {
        private const float DAY_LENGTH_SECONDS = 600f;

        private static readonly Color NightAmbientColor = new(0.08f, 0.1f, 0.18f);
        private static readonly Color DawnAmbientColor = new(0.42f, 0.36f, 0.3f);
        private static readonly Color DayAmbientColor = new(0.95f, 0.98f, 1f);
        private static readonly Color DuskAmbientColor = new(0.5f, 0.32f, 0.26f);

        private static readonly Color NightSkyColor = new(0.02f, 0.03f, 0.08f);
        private static readonly Color DawnSkyColor = new(0.92f, 0.52f, 0.3f);
        private static readonly Color DaySkyColor = new(0.53f, 0.81f, 0.92f);
        private static readonly Color DuskSkyColor = new(0.88f, 0.42f, 0.26f);

        private bool _wasNight;

        public enum Weather { Clear, Rain, Overcast, Storm }

        public float TimeOfDay01 { get; private set; }
        public Weather CurrentWeather { get; private set; } = Weather.Clear;
        public int DayNumber { get; private set; }
        public bool IsNight => TimeOfDay01 > 0.75f || TimeOfDay01 < 0.25f;

        public event Action OnSunrise;
        public event Action OnSunset;
        public event Action OnNewDay;

        private void Start()
        {
            DayNumber = 1;
            _wasNight = IsNight;
            ApplyLighting();
        }

        private void Update()
        {
            TimeOfDay01 += Time.deltaTime / DAY_LENGTH_SECONDS;

            // Occasional weather change
            if (Random.value < 0.001f) // ~every 1000 seconds
            {
                CurrentWeather = (Weather)Random.Range(0, 4);
            }

            if (TimeOfDay01 >= 1f)
            {
                TimeOfDay01 -= 1f;
                DayNumber++;
                OnNewDay?.Invoke();
            }

            var isNight = IsNight;
            if (_wasNight && !isNight)
            {
                OnSunrise?.Invoke();
            }
            else if (!_wasNight && isNight)
            {
                OnSunset?.Invoke();
            }

            _wasNight = isNight;
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            var ambientColor = EvaluateAmbientColor(TimeOfDay01);
            var skyColor = EvaluateSkyColor(TimeOfDay01);
            var ambientIntensity = EvaluateAmbientIntensity(TimeOfDay01);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.ambientIntensity = ambientIntensity;

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.backgroundColor = skyColor;
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private static Color EvaluateAmbientColor(float timeOfDay01)
        {
            if (timeOfDay01 < 0.25f)
            {
                return Color.Lerp(NightAmbientColor, DawnAmbientColor, Mathf.InverseLerp(0f, 0.25f, timeOfDay01));
            }

            if (timeOfDay01 < 0.5f)
            {
                return Color.Lerp(DawnAmbientColor, DayAmbientColor, Mathf.InverseLerp(0.25f, 0.5f, timeOfDay01));
            }

            if (timeOfDay01 < 0.75f)
            {
                return Color.Lerp(DayAmbientColor, DuskAmbientColor, Mathf.InverseLerp(0.5f, 0.75f, timeOfDay01));
            }

            return Color.Lerp(DuskAmbientColor, NightAmbientColor, Mathf.InverseLerp(0.75f, 1f, timeOfDay01));
        }

        private static Color EvaluateSkyColor(float timeOfDay01)
        {
            if (timeOfDay01 < 0.25f)
            {
                return Color.Lerp(NightSkyColor, DawnSkyColor, Mathf.InverseLerp(0f, 0.25f, timeOfDay01));
            }

            if (timeOfDay01 < 0.5f)
            {
                return Color.Lerp(DawnSkyColor, DaySkyColor, Mathf.InverseLerp(0.25f, 0.5f, timeOfDay01));
            }

            if (timeOfDay01 < 0.75f)
            {
                return Color.Lerp(DaySkyColor, DuskSkyColor, Mathf.InverseLerp(0.5f, 0.75f, timeOfDay01));
            }

            return Color.Lerp(DuskSkyColor, NightSkyColor, Mathf.InverseLerp(0.75f, 1f, timeOfDay01));
        }

        private static float EvaluateAmbientIntensity(float timeOfDay01)
        {
            if (timeOfDay01 < 0.25f)
            {
                return Mathf.Lerp(0.2f, 0.55f, Mathf.InverseLerp(0f, 0.25f, timeOfDay01));
            }

            if (timeOfDay01 < 0.5f)
            {
                return Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(0.25f, 0.5f, timeOfDay01));
            }

            if (timeOfDay01 < 0.75f)
            {
                return Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(0.5f, 0.75f, timeOfDay01));
            }

            return Mathf.Lerp(0.45f, 0.2f, Mathf.InverseLerp(0.75f, 1f, timeOfDay01));
        }
    }
}
