using UnityEngine;
using CommandP.Core;
using CommandP.Data.DTOs;
using CommandP.Data.Stores;
using System.Collections.Generic;
using System.Linq;

namespace CommandP.Test
{
    public class UIManager : MonoBehaviour
    {
        private Vector2 _eventScroll;
        private Vector2 _aircraftScroll;
        private Vector2 _satelliteScroll;
        private string _selectedAircraftId;
        private string _selectedSatelliteId;
        private UnitViewManager _unitViewManager;
        private AircraftSimulator _aircraftSimulator;
        private SatelliteSimulator _satelliteSimulator;
        private Vector3 _aircraftRotationOffset;
        private bool _hasSyncedAircraftOffset;
        private Vector3 _cameraFollowOffset;
        private bool _hasSyncedCameraFollowOffset;
        private MissileStrikeDemo _missileDemoController;
        private Vector3 _missileRotationOffset;
        private bool _hasSyncedMissileOffset;

        void OnGUI()
        {
            return; // Disabled
            var appMgr = AppManager.Instance;
            if (_unitViewManager == null)
            {
                _unitViewManager = FindFirstObjectByType<UnitViewManager>();
            }

            if (_unitViewManager != null && !_hasSyncedAircraftOffset)
            {
                _aircraftRotationOffset = _unitViewManager.GetAircraftRotationOffsetEuler();
                _hasSyncedAircraftOffset = true;
            }

            if (_unitViewManager != null && !_hasSyncedCameraFollowOffset)
            {
                _cameraFollowOffset = _unitViewManager.GetCameraFollowViewOffset();
                _hasSyncedCameraFollowOffset = true;
            }

            if (_missileDemoController == null)
            {
                _missileDemoController = FindFirstObjectByType<MissileStrikeDemo>();
            }

            if (_missileDemoController != null && !_hasSyncedMissileOffset)
            {
                _missileRotationOffset = _missileDemoController.GetMissileRotationOffset();
                _hasSyncedMissileOffset = true;
            }

            GUILayout.BeginArea(new Rect(10, 10, 420, 640));
            GUILayout.Box("CommandP Military Simulation - Phase 1", GUILayout.Width(380));

            if (appMgr != null)
            {
                var unitStore = appMgr.GetUnitStore();
                if (_aircraftSimulator == null)
                {
                    _aircraftSimulator = appMgr.GetAircraftSimulator();
                }
                if (_satelliteSimulator == null)
                {
                    _satelliteSimulator = appMgr.GetSatelliteSimulator();
                }

                GUILayout.Space(6);
                GUILayout.Label("Simulation Active (Aircraft + Satellite)", GUI.skin.textField);
                GUILayout.Label($"Units Total: {unitStore.GetTotalUnitCount()}");
                GUILayout.Label($"Aircraft Simulator: {(_aircraftSimulator != null ? (_aircraftSimulator.IsPaused ? "Paused" : "Running") : "Missing")}");
                GUILayout.Label($"Satellite Simulator: {(_satelliteSimulator != null ? (_satelliteSimulator.IsPaused ? "Paused" : "Running") : "Missing")}");

                if (_aircraftSimulator != null || _satelliteSimulator != null)
                {
                    string buttonText = appMgr.IsSimulationPaused() ? "Resume Simulation" : "Pause Simulation";
                    if (GUILayout.Button(buttonText, GUILayout.Width(160)))
                    {
                        appMgr.ToggleSimulationPaused();
                    }
                }

                GUILayout.Space(8);
                DrawAircraftOffsetPanel();

                GUILayout.Space(8);
                DrawCameraFollowViewPanel();

                GUILayout.Space(10);
                DrawMissileStrikeDemoPanel();

                GUILayout.Space(10);

                GUILayout.Label("Moving Units Info:", GUI.skin.box);
                DrawAircraftInfoPanel(unitStore);
            }
            else
            {
                GUILayout.Label("AppManager not found!");
            }

            GUILayout.EndArea();

            DrawAircraftPanel(appMgr);
        }

        private void DrawAircraftOffsetPanel()
        {
            GUILayout.Label("Aircraft Rotation Offset (Runtime)", GUI.skin.box);

            if (_unitViewManager == null)
            {
                GUILayout.Label("UnitViewManager not found.");
                return;
            }

            float newX = DrawOffsetSlider("X", _aircraftRotationOffset.x, -180f, 180f);
            float newY = DrawOffsetSlider("Y", _aircraftRotationOffset.y, -180f, 180f);
            float newZ = DrawOffsetSlider("Z", _aircraftRotationOffset.z, -180f, 180f);

            Vector3 newOffset = new Vector3(newX, newY, newZ);
            if (newOffset != _aircraftRotationOffset)
            {
                _aircraftRotationOffset = newOffset;
                _unitViewManager.SetAircraftRotationOffsetEuler(_aircraftRotationOffset);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Value: ({_aircraftRotationOffset.x:F1}, {_aircraftRotationOffset.y:F1}, {_aircraftRotationOffset.z:F1})");
            if (GUILayout.Button("Reset", GUILayout.Width(70)))
            {
                _aircraftRotationOffset = Vector3.zero;
                _unitViewManager.ResetAircraftRotationOffsetEuler();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCameraFollowViewPanel()
        {
            GUILayout.Label("Camera Follow View (Runtime)", GUI.skin.box);

            if (_unitViewManager == null)
            {
                GUILayout.Label("UnitViewManager not found.");
                return;
            }

            float newX = DrawOffsetSlider("X", _cameraFollowOffset.x, -5000f, 5000f);
            float newY = DrawOffsetSlider("Y", _cameraFollowOffset.y, -5000f, 5000f);
            float newZ = DrawOffsetSlider("Z", _cameraFollowOffset.z, 100f, 20000f);

            Vector3 newOffset = new Vector3(newX, newY, newZ);
            if (newOffset != _cameraFollowOffset)
            {
                _cameraFollowOffset = newOffset;
                _unitViewManager.SetCameraFollowViewOffset(_cameraFollowOffset);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Value: ({_cameraFollowOffset.x:F1}, {_cameraFollowOffset.y:F1}, {_cameraFollowOffset.z:F1})");
            if (GUILayout.Button("Reset", GUILayout.Width(70)))
            {
                _cameraFollowOffset = new Vector3(0f, 2000f, 1200f);
                _unitViewManager.ResetCameraFollowViewOffset();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("X=left/right, Y=up/down, Z=follow distance");
        }

        private float DrawOffsetSlider(string label, float currentValue, float minValue, float maxValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(18));
            float sliderValue = GUILayout.HorizontalSlider(currentValue, minValue, maxValue, GUILayout.Width(240));
            GUILayout.Label(sliderValue.ToString("F1"), GUILayout.Width(44));
            GUILayout.EndHorizontal();
            return sliderValue;
        }

        private void DrawAircraftPanel(AppManager appMgr)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 310, Screen.height - 20));
            GUILayout.Box("Moving Units", GUILayout.Width(290));

            if (appMgr == null)
            {
                GUILayout.Label("AppManager not found!");
                GUILayout.EndArea();
                return;
            }

            var unitStore = appMgr.GetUnitStore();
            DrawUnitListSection(unitStore, "Aircraft", ref _aircraftScroll, ref _selectedAircraftId);
            GUILayout.Space(10);
            DrawUnitListSection(unitStore, "Satellite", ref _satelliteScroll, ref _selectedSatelliteId);
            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();
        }

        private void DrawUnitListSection(UnitStore unitStore, string typeName, ref Vector2 scroll, ref string selectedId)
        {
            var units = new List<CachedUnit>();
            foreach (var unit in unitStore.GetAllUnits().Values)
            {
                if (unit == null) continue;
                if (!string.Equals(unit.Type, typeName, System.StringComparison.OrdinalIgnoreCase)) continue;
                units.Add(unit);
            }

            units = units.OrderBy(u => u.Name).ToList();

            GUILayout.Label($"{typeName} Total: {units.Count}");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(300), GUILayout.Height((Screen.height - 170) * 0.38f));
            if (units.Count == 0)
            {
                GUILayout.Label($"No {typeName.ToLowerInvariant()} found.");
            }
            else
            {
                foreach (var unit in units)
                {
                    bool selected = selectedId == unit.ObjectID;
                    GUI.backgroundColor = selected ? new Color(0.25f, 0.55f, 0.9f) : Color.white;

                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"{GetUnitIcon(typeName)} {unit.Name}");
                    GUILayout.Label($"DBID: {unit.DBID}");
                    if (unit.Position != null)
                    {
                        GUILayout.Label($"Lat: {unit.Position.Latitude:0.00000}");
                        GUILayout.Label($"Lon: {unit.Position.Longitude:0.00000}");
                        GUILayout.Label($"Alt: {unit.Position.Altitude:0}");
                    }

                    if (GUILayout.Button("Jump To Latest Position"))
                    {
                        selectedId = unit.ObjectID;
                        if (_unitViewManager == null)
                        {
                            _unitViewManager = FindFirstObjectByType<UnitViewManager>();
                        }

                        if (_unitViewManager != null)
                        {
                            _unitViewManager.FocusUnitOnCamera(unit.ObjectID);
                        }
                    }

                    string followButtonText = _unitViewManager != null && _unitViewManager.IsCameraFollowing(unit.ObjectID)
                        ? "Unfollow Camera"
                        : "Follow Camera";

                    if (GUILayout.Button(followButtonText))
                    {
                        selectedId = unit.ObjectID;
                        if (_unitViewManager == null)
                        {
                            _unitViewManager = FindFirstObjectByType<UnitViewManager>();
                        }

                        if (_unitViewManager != null)
                        {
                            if (_unitViewManager.IsCameraFollowing(unit.ObjectID))
                            {
                                _unitViewManager.ClearCameraFollow();
                            }
                            else
                            {
                                _unitViewManager.FollowUnitOnCamera(unit.ObjectID);
                            }
                        }
                    }

                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }
            }

            GUILayout.EndScrollView();
        }

        private static string GetUnitIcon(string typeName)
        {
            if (string.Equals(typeName, "Satellite", System.StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return "";
        }

        private void DrawAircraftInfoPanel(UnitStore unitStore)
        {
            var allUnits = unitStore.GetAllUnits();
            if (allUnits.Count == 0)
            {
                GUILayout.Label("No aircraft data yet. Waiting for simulator...");
                GUILayout.Label("(Simulator starts generating data shortly)");
                return;
            }

            foreach (var kv in allUnits)
            {
                var unit = kv.Value;
                if (unit == null || (!string.Equals(unit.Type, "Aircraft", System.StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(unit.Type, "Satellite", System.StringComparison.OrdinalIgnoreCase)))
                    continue;

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{GetUnitIcon(unit.Type)} {unit.Name}  (DBID: {unit.DBID})");
                GUILayout.Label($"Status: {unit.Status?.Primary ?? "N/A"}");
                if (unit.Position != null)
                {
                    GUILayout.Label($"Lat: {unit.Position.Latitude:F5}掳");
                    GUILayout.Label($"Lon: {unit.Position.Longitude:F5}掳");
                    GUILayout.Label($"Alt: {unit.Position.Altitude:F0} m");
                }
                if (unit.Movement != null)
                {
                    GUILayout.Label($"Speed: {unit.Movement.Speed:F0} kn");
                    GUILayout.Label($"Heading: {unit.Movement.Heading:F1}掳");
                }
                GUILayout.EndVertical();
            }
        }

        private void DrawEventCard(GameEvent evt)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"ID: {evt.EventID}");
            GUILayout.Label($"Type: {evt.Type}");
            GUILayout.Label($"Time: {evt.Timestamp}");
            GUILayout.Label($"Severity: {evt.Severity}");
            GUILayout.Label($"Detail: {evt.Description}", GUILayout.ExpandHeight(true));
            GUILayout.EndVertical();
        }

        private void DrawMissileStrikeDemoPanel()
        {
            GUILayout.Label("Missile Strike Demo", GUI.skin.box);

            if (_missileDemoController == null)
            {
                GUILayout.Label("MissileStrikeDemo not found in scene.");
                if (GUILayout.Button("Create Missile Demo"))
                {
                    GameObject demoPrefab = new GameObject("MissileStrikeDemo");
                    _missileDemoController = demoPrefab.AddComponent<MissileStrikeDemo>();
                    
                    // 鎶婂畠鏀惧湪 CesiumGeoreference 涓?
                    var georeference = FindFirstObjectByType<CesiumForUnity.CesiumGeoreference>();
                    if (georeference != null)
                    {
                        demoPrefab.transform.SetParent(georeference.transform);
                    }
                }
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("鈻?Launch Missile", GUILayout.Height(30)))
            {
                _missileDemoController.LaunchDemo();
            }

            if (GUILayout.Button("馃帴 View Strike Zone", GUILayout.Height(30)))
            {
                LocateCameraToStrikeZone();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _missileDemoController.IsFlying;
            string pauseButtonText = _missileDemoController.IsPaused ? "鈻?Resume" : "鈴?Pause";
            if (GUILayout.Button(pauseButtonText, GUILayout.Height(28)))
            {
                _missileDemoController.TogglePause();
            }
            GUI.enabled = true;

            GUILayout.Label(_missileDemoController.IsFlying
                ? (_missileDemoController.IsPaused ? "Flight paused" : "Flight running")
                : "Missile idle");
            GUILayout.EndHorizontal();

            GUILayout.Label("Missile Model Offset (Runtime)", GUI.skin.box);

            float newX = DrawOffsetSlider("X", _missileRotationOffset.x, -180f, 180f);
            float newY = DrawOffsetSlider("Y", _missileRotationOffset.y, -180f, 180f);
            float newZ = DrawOffsetSlider("Z", _missileRotationOffset.z, -180f, 180f);

            Vector3 newOffset = new Vector3(newX, newY, newZ);
            if (newOffset != _missileRotationOffset)
            {
                _missileRotationOffset = newOffset;
                _missileDemoController.SetMissileRotationOffset(_missileRotationOffset);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Value: ({_missileRotationOffset.x:F1}, {_missileRotationOffset.y:F1}, {_missileRotationOffset.z:F1})");
            if (GUILayout.Button("Reset", GUILayout.Width(70)))
            {
                _missileRotationOffset = Vector3.zero;
                _missileDemoController.SetMissileRotationOffset(Vector3.zero);
            }
            GUILayout.EndHorizontal();

            if (_missileDemoController.IsFlying)
            {
                GUILayout.Label("馃殌 Missile in flight...", GUI.skin.textField);
            }
        }

        private float DrawMissileRotationSlider(string label, float currentValue, float minValue, float maxValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60));
            float sliderValue = GUILayout.HorizontalSlider(currentValue, minValue, maxValue, GUILayout.Width(150));
            GUILayout.Label(sliderValue.ToString("F0") + "掳", GUILayout.Width(40));
            GUILayout.EndHorizontal();
            return sliderValue;
        }

        private void LocateCameraToStrikeZone()
        {
            if (_missileDemoController == null)
                return;

            var mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            // 鑾峰彇鎵撳嚮鍖哄煙鐨勮鐐?
            Vector3 viewPoint = _missileDemoController.GetStrikeViewPoint();
            Vector3 viewDirection = _missileDemoController.GetStrikeViewDirection();

            // 绉诲姩鐩告満
            mainCamera.transform.position = viewPoint;
            mainCamera.transform.LookAt(mainCamera.transform.position + viewDirection, Vector3.up);

            Debug.Log("[UIManager] Camera positioned to view strike area");
        }
    }
}

