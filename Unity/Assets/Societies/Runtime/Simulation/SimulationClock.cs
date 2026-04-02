using System;
using System.Diagnostics;
using UnityEngine;

namespace Societies.Runtime.Simulation
{
    /// <summary>
    /// Fixed tick loop running at 20 TPS (50ms per tick).
    /// All simulation logic should hook into this system.
    /// </summary>
    public sealed class SimulationClock : MonoBehaviour
    {
        public static SimulationClock Instance { get; private set; }

        // 20 TPS = 50ms per tick
        private const float TICK_RATE = 20f;
        private const float TICK_DURATION = 1f / TICK_RATE; // 0.05f
        private const float MAX_TICK_BUDGET_MS = 40f; // Leave 10ms safety margin
        private const float TICK_WARNING_THRESHOLD_MS = 45f;

        private readonly Stopwatch _tickTimer = new();
        private ulong _currentTick;
        private bool _isRunning;
        private float _accumulatedTime;

        // Tick phase delegates
        public event Action OnEarlyUpdate;
        public event Action OnWorldUpdate;
        public event Action OnAIUpdate;
        public event Action OnEconomyUpdate;
        public event Action OnLateUpdate;

        // Metrics
        public float LastTickDurationMs { get; private set; }
        public float AverageTickDurationMs { get; private set; }
        public int TickOverBudgetCount { get; private set; }

        public ulong CurrentTick => _currentTick;
        public float TickDuration => TICK_DURATION;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _isRunning = true;
            _tickTimer.Start();
            UnityEngine.Debug.Log($"[SimulationClock] Started at {TICK_RATE} TPS ({TICK_DURATION * 1000f:F1}ms per tick)");
        }

        private void OnDestroy()
        {
            _isRunning = false;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isRunning) return;

            _accumulatedTime += Time.deltaTime;

            while (_accumulatedTime >= TICK_DURATION)
            {
                _accumulatedTime -= TICK_DURATION;
                Tick();
            }
        }

        private void Tick()
        {
            _tickTimer.Restart();

            // Phase 1: Early update (input, player state prep)
            OnEarlyUpdate?.Invoke();

            // Phase 2: World simulation (blocks, physics, chunk updates)
            OnWorldUpdate?.Invoke();

            // Phase 3: AI simulation (agents, needs, goals)
            OnAIUpdate?.Invoke();

            // Phase 4: Economy simulation (trading, transactions)
            OnEconomyUpdate?.Invoke();

            // Phase 5: Late update (cleanup, persistence triggers)
            OnLateUpdate?.Invoke();

            _tickTimer.Stop();
            LastTickDurationMs = _tickTimer.ElapsedTicks * 1000f / Stopwatch.Frequency;
            
            // Rolling average (last 60 ticks)
            AverageTickDurationMs = Mathf.Lerp(AverageTickDurationMs, LastTickDurationMs, 0.016f);

            if (LastTickDurationMs > TICK_WARNING_THRESHOLD_MS)
            {
                TickOverBudgetCount++;
                UnityEngine.Debug.LogWarning($"[SimulationClock] Tick {_currentTick} took {LastTickDurationMs:F1}ms (budget: {MAX_TICK_BUDGET_MS}ms)");
            }
            else if (LastTickDurationMs > MAX_TICK_BUDGET_MS)
            {
                TickOverBudgetCount++;
            }

            _currentTick++;
        }

        /// <summary>
        /// Get simulation time in seconds from tick count
        /// </summary>
        public double GetSimTimeSeconds() => _currentTick * TICK_DURATION;

        /// <summary>
        /// Convert real time to tick count
        /// </summary>
        public static ulong RealTimeToTick(float realTimeSeconds) => 
            (ulong)(realTimeSeconds * TICK_RATE);
    }
}