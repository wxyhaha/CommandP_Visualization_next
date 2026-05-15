using System.Collections.Generic;
using UnityEngine;
using CommandP.Data.DTOs;

namespace CommandP.Data.Stores
{
    public class SideStore
    {
        private Dictionary<int, SideOverview> _sidesMap = new Dictionary<int, SideOverview>();

        public void MergeOverview(ScenarioOverviewResponse overview)
        {
            if (overview?.Sides == null) return;

            _sidesMap.Clear();

            for (int i = 0; i < overview.Sides.Count; i++)
            {
                _sidesMap[i] = overview.Sides[i];
            }
        }

        public SideOverview GetSide(int sideId)
        {
            return _sidesMap.TryGetValue(sideId, out var side) ? side : null;
        }

        public Dictionary<int, SideOverview> GetAllSides() => _sidesMap;

        public int GetSideCount() => _sidesMap.Count;
    }
}
