using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CommandP.Data.DTOs;

namespace CommandP.Data.Stores
{
    public class UnitStore
    {
        private Dictionary<string, CachedUnit> _unitMap = new Dictionary<string, CachedUnit>();
        private Dictionary<int, List<CachedUnit>> _unitsBySide = new Dictionary<int, List<CachedUnit>>();

        public System.Action<string, CachedUnit> OnUnitAdded;
        public System.Action<string, CachedUnit> OnUnitUpdated;
        public System.Action<string> OnUnitDestroyed;

        public void MergeOverview(ScenarioOverviewResponse overview)
        {
            if (overview?.Sides == null) return;

            var newUnitIds = new HashSet<string>();
            int sideId = 0;

            foreach (var side in overview.Sides)
            {
                sideId++;
                if (!_unitsBySide.ContainsKey(sideId))
                    _unitsBySide[sideId] = new List<CachedUnit>();

                _unitsBySide[sideId].Clear();

                if (side.Groups != null)
                {
                    foreach (var group in side.Groups)
                    {
                        if (group.Units != null)
                        {
                            foreach (var unitDetail in group.Units)
                            {
                                newUnitIds.Add(unitDetail.ObjectID);
                                MergeUnit(unitDetail, sideId);
                                _unitsBySide[sideId].Add(_unitMap[unitDetail.ObjectID]);
                            }
                        }
                    }
                }

                if (side.UngroupedUnits != null)
                {
                    foreach (var unitDetail in side.UngroupedUnits)
                    {
                        newUnitIds.Add(unitDetail.ObjectID);
                        MergeUnit(unitDetail, sideId);
                        _unitsBySide[sideId].Add(_unitMap[unitDetail.ObjectID]);
                    }
                }
            }

            var deletedIds = _unitMap.Keys.Except(newUnitIds).ToList();
            foreach (var id in deletedIds)
            {
                _unitMap.Remove(id);
                OnUnitDestroyed?.Invoke(id);
            }

        }

        private void MergeUnit(UnitDetail detail, int sideId)
        {
            if (detail == null) return;

            float newMaxSensorRangeNm = GetMaxSensorRangeNm(detail.Sensors);

            if (_unitMap.TryGetValue(detail.ObjectID, out var existing))
            {
                bool posChanged = !PositionsEqual(existing.Position, detail.RealtimeState?.Position);
                bool statusChanged = existing.Status?.IsDestroyed != detail.RealtimeState?.Status?.IsDestroyed
                                   || existing.Status?.DamageLevel != detail.RealtimeState?.Status?.DamageLevel;
                bool sensorChanged = Mathf.Abs(existing.MaxSensorRangeNm - newMaxSensorRangeNm) > 0.01f;

                if (posChanged || statusChanged || sensorChanged)
                {
                    existing.Position = detail.RealtimeState?.Position;
                    existing.Movement = detail.RealtimeState?.Movement;
                    existing.Status = detail.RealtimeState?.Status;
                    existing.Sensors = detail.Sensors;
                    existing.MaxSensorRangeNm = newMaxSensorRangeNm;
                    existing.HasPositionChanged = posChanged;
                    existing.HasStatusChanged = statusChanged;

                    OnUnitUpdated?.Invoke(existing.ObjectID, existing);
                }
            }
            else
            {
                var cached = new CachedUnit
                {
                    ObjectID = detail.ObjectID,
                    Name = detail.Name,
                    Type = detail.Type,
                    DBID = detail.DBID,
                    Position = detail.RealtimeState?.Position,
                    Movement = detail.RealtimeState?.Movement,
                    Status = detail.RealtimeState?.Status,
                    Sensors = detail.Sensors,
                    MaxSensorRangeNm = newMaxSensorRangeNm,
                    SideID = sideId,
                    HasPositionChanged = true,
                    HasStatusChanged = true
                };

                _unitMap[detail.ObjectID] = cached;
                OnUnitAdded?.Invoke(detail.ObjectID, cached);
            }
        }

        private static float GetMaxSensorRangeNm(List<SensorDetail> sensors)
        {
            if (sensors == null || sensors.Count == 0)
            {
                return 0f;
            }

            float max = 0f;
            foreach (var s in sensors)
            {
                if (s != null && s.MaxRange > max)
                {
                    max = s.MaxRange;
                }
            }

            return max;
        }

        private bool PositionsEqual(Position a, Position b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return Math.Abs(a.Latitude - b.Latitude) < 0.0001f
                && Math.Abs(a.Longitude - b.Longitude) < 0.0001f
                && Math.Abs(a.Altitude - b.Altitude) < 0.1f;
        }

        public CachedUnit GetUnit(string objectId)
        {
            return _unitMap.TryGetValue(objectId, out var unit) ? unit : null;
        }

        public List<CachedUnit> GetUnitsBySide(int sideId)
        {
            return _unitsBySide.TryGetValue(sideId, out var units) ? units : new List<CachedUnit>();
        }

        public int GetTotalUnitCount() => _unitMap.Count;

        public Dictionary<string, CachedUnit> GetAllUnits() => _unitMap;

        /// <summary>
        /// 直接添加或更新一个单位（供模拟器使用）
        /// </summary>
        public void AddOrUpdateUnit(CachedUnit unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.ObjectID)) return;

            if (_unitMap.TryGetValue(unit.ObjectID, out var existing))
            {
                // 更新
                bool posChanged = unit.Position != null && !PositionsEqual(existing.Position, unit.Position);
                bool statusChanged = (unit.Status?.IsDestroyed != existing.Status?.IsDestroyed)
                                  || (unit.Status?.DamageLevel != existing.Status?.DamageLevel);

                existing.Position = unit.Position ?? existing.Position;
                existing.Movement = unit.Movement ?? existing.Movement;
                existing.Status = unit.Status ?? existing.Status;
                existing.Sensors = unit.Sensors ?? existing.Sensors;
                existing.MaxSensorRangeNm = unit.MaxSensorRangeNm;
                existing.HasPositionChanged = posChanged;
                existing.HasStatusChanged = statusChanged;

                OnUnitUpdated?.Invoke(existing.ObjectID, existing);
            }
            else
            {
                unit.SideID = unit.SideID > 0 ? unit.SideID : 1;
                unit.HasPositionChanged = true;
                unit.HasStatusChanged = true;
                _unitMap[unit.ObjectID] = unit;

                // 注册到方阵列表
                int sid = unit.SideID;
                if (!_unitsBySide.ContainsKey(sid))
                    _unitsBySide[sid] = new List<CachedUnit>();
                _unitsBySide[sid].Add(unit);

                OnUnitAdded?.Invoke(unit.ObjectID, unit);
            }
        }
    }
}
