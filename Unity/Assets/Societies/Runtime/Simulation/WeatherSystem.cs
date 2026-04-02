using System;
using UnityEngine;

namespace Societies.Runtime.Simulation
{
    public enum WeatherType
    {
        Clear,
        Cloudy,
        Rain,
        Storm,
        Fog
    }

    /// <summary>
    /// Manages periodic weather transitions for the simulation.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        [Header("Weather Timing")]
        [SerializeField] private float _minWeatherDurationSeconds = 90f;
        [SerializeField] private float _maxWeatherDurationSeconds = 240f;
        [SerializeField, Range(0f, 1f)] private float _stormChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _rainChance = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _fogChance = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _cloudyChance = 0.28f;

        private float _weatherTimer;
        private float _nextWeatherChangeTime;

        public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
        public WeatherType PreviousWeather { get; private set; } = WeatherType.Clear;
        public float TimeUntilNextChange => Mathf.Max(0f, _nextWeatherChangeTime - _weatherTimer);

        public event Action<WeatherType, WeatherType> OnWeatherChanged;

        private void Start()
        {
            ScheduleNextWeatherChange();
        }

        private void Update()
        {
            _weatherTimer += Time.deltaTime;

            if (_weatherTimer < _nextWeatherChangeTime)
            {
                return;
            }

            ChangeWeather(SelectNextWeather());
        }

        public void ForceWeather(WeatherType newWeather)
        {
            ChangeWeather(newWeather);
        }

        private void ChangeWeather(WeatherType newWeather)
        {
            PreviousWeather = CurrentWeather;
            CurrentWeather = newWeather;
            _weatherTimer = 0f;
            ScheduleNextWeatherChange();

            if (PreviousWeather != CurrentWeather)
            {
                UnityEngine.Debug.Log($"[WeatherSystem] Weather changed from {PreviousWeather} to {CurrentWeather}");
                OnWeatherChanged?.Invoke(PreviousWeather, CurrentWeather);
            }
        }

        private WeatherType SelectNextWeather()
        {
            var roll = UnityEngine.Random.value;

            if (roll < _stormChance)
            {
                return WeatherType.Storm;
            }

            roll -= _stormChance;
            if (roll < _rainChance)
            {
                return WeatherType.Rain;
            }

            roll -= _rainChance;
            if (roll < _fogChance)
            {
                return WeatherType.Fog;
            }

            roll -= _fogChance;
            if (roll < _cloudyChance)
            {
                return WeatherType.Cloudy;
            }

            return WeatherType.Clear;
        }

        private void ScheduleNextWeatherChange()
        {
            _nextWeatherChangeTime = UnityEngine.Random.Range(_minWeatherDurationSeconds, _maxWeatherDurationSeconds);
        }
    }
}
