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

        public void SetWeatherLightMultiplier(float multiplier)
        {
            _weatherLightMultiplier = multiplier;
            UpdateLighting();
        }

        public void ApplyState(float currentHour, float weatherLightMultiplier)
        {
            CurrentHour = currentHour;
            _weatherLightMultiplier = weatherLightMultiplier;
            UpdateLighting();
        }

        public string GetTimeText()
        {
            return PrototypeClockService.FormatTime(CurrentHour);
        }

        private void UpdateLighting()
        {
            PrototypeLightingState lighting = PrototypeClockService.CalculateLighting(CurrentHour, _weatherLightMultiplier);

            if (_sunLight != null)
            {
                _sunLight.RotationDegrees = lighting.SunRotationDegrees;
                _sunLight.LightEnergy = lighting.SunEnergy;
                _sunLight.LightColor = lighting.SunColor;
            }

            RenderingServer.SetDefaultClearColor(lighting.ClearColor);
        }
    }
}
