using UnityEngine;
using CommandP.Data.Stores;

namespace CommandP.Core
{
    public class AppManager : MonoBehaviour
    {
        public static AppManager Instance { get; private set; }

        private AircraftSimulator _simulator;
        private SatelliteSimulator _satelliteSimulator;
        private GroundRadarDomeController _groundRadarDomeController;
        private UnitStore _unitStore;
        private EventStore _eventStore;
        private SideStore _sideStore;
        private bool _simulationPaused;

        private int _unitAddedCount = 0;
        private int _unitUpdatedCount = 0;
        private int _unitDestroyedCount = 0;

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            InitializeServices();
            SubscribeToEvents();
        }

        private void InitializeServices()
        {
            _simulator = gameObject.AddComponent<AircraftSimulator>();
            _satelliteSimulator = gameObject.AddComponent<SatelliteSimulator>();
            _groundRadarDomeController = gameObject.AddComponent<GroundRadarDomeController>();

            _unitStore = new UnitStore();
            _eventStore = new EventStore();
            _sideStore = new SideStore();
        }

        private void SubscribeToEvents()
        {
            _unitStore.OnUnitAdded += (id, unit) =>
            {
                _unitAddedCount++;
                Debug.Log($"[AppManager] Unit added: {unit.Name} ({id})");
            };

            _unitStore.OnUnitUpdated += (id, unit) =>
            {
                _unitUpdatedCount++;
            };

            _unitStore.OnUnitDestroyed += (id) =>
            {
                _unitDestroyedCount++;
                Debug.Log($"[AppManager] Unit destroyed: {id}");
            };
        }

        public UnitStore GetUnitStore() => _unitStore;
        public EventStore GetEventStore() => _eventStore;
        public SideStore GetSideStore() => _sideStore;
        public AircraftSimulator GetAircraftSimulator() => _simulator;
        public SatelliteSimulator GetSatelliteSimulator() => _satelliteSimulator;
        public GroundRadarDomeController GetGroundRadarDomeController() => _groundRadarDomeController;
        public bool IsSimulationPaused() => _simulationPaused;

        public void SetSimulationPaused(bool paused)
        {
            _simulationPaused = paused;

            if (_simulator != null)
            {
                _simulator.SetPaused(paused);
            }

            if (_satelliteSimulator != null)
            {
                _satelliteSimulator.SetPaused(paused);
            }
        }

        public void ToggleSimulationPaused()
        {
            SetSimulationPaused(!_simulationPaused);
        }
    }
}
