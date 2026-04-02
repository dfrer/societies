using Godot;

namespace Societies.Simulation
{
    /// <summary>
    /// Handles the world clock and simple sun lighting for Prototype 1.
    /// </summary>
    public partial class DayNightCycle : Node3D
    {
        [Export] public float DayLengthSeconds { get; set; } = 300.0f;
        [Export] public float StartHour { get; set; } = 8.0f;

        private DirectionalLight3D? _sunLight;
        private float _weatherLightMultiplier = 1.0f;

        public float CurrentHour { get; private set; }

        public override void _Ready()
        {
            CurrentHour = StartHour;

            _sunLight = new DirectionalLight3D
            {
                Name = "SunLight",
                LightEnergy = 1.2f,
                ShadowEnabled = true,
                Position = new Vector3(0.0f, 40.0f, 0.0f)
            };
            AddChild(_sunLight);

            UpdateLighting();
        }

        public override void _Process(double delta)
        {
            float hoursPerSecond = 24.0f / DayLengthSeconds;
            CurrentHour = Mathf.PosMod(CurrentHour + ((float)delta * hoursPerSecond), 24.0f);
            UpdateLighting();
        }

        public void SetWeatherLightMultiplier(float multiplier)
        {
            _weatherLightMultiplier = multiplier;
            UpdateLighting();
        }

        public string GetTimeText()
        {
            int hours = Mathf.FloorToInt(CurrentHour);
            int minutes = Mathf.FloorToInt((CurrentHour - hours) * 60.0f);
            return $"{hours:00}:{minutes:00}";
        }

        private void UpdateLighting()
        {
            float normalized = CurrentHour / 24.0f;
            float sunPhase = Mathf.Sin((normalized * Mathf.Tau) - (Mathf.Pi * 0.5f));
            float daylight = Mathf.Clamp((sunPhase + 1.0f) * 0.5f, 0.08f, 1.0f);

            if (_sunLight != null)
            {
                _sunLight.RotationDegrees = new Vector3((normalized * 360.0f) - 90.0f, -35.0f, 0.0f);
                _sunLight.LightEnergy = Mathf.Lerp(0.15f, 1.25f, daylight) * _weatherLightMultiplier;
                _sunLight.LightColor = new Color(1.0f, 0.9f, 0.78f).Lerp(new Color(0.56f, 0.63f, 0.92f), 1.0f - daylight);
            }

            RenderingServer.SetDefaultClearColor(
                new Color(0.04f, 0.07f, 0.18f).Lerp(new Color(0.5f, 0.74f, 0.94f), daylight)
            );
        }
    }
}
