using System;
using System.Collections.Generic;
using UnityEngine;
using CommandP.Data.DTOs;

namespace CommandP.Data.Stores
{
    public class EventStore
    {
        private HashSet<string> _processedEventIds = new HashSet<string>();
        private List<GameEvent> _allEvents = new List<GameEvent>();

        public System.Action<GameEvent> OnNewEvent;
        public int ProcessedEventCount => _processedEventIds.Count;

        public void ProcessEvents(EventsResponse response)
        {
            if (response?.Events == null) return;

            int newEventsCount = 0;

            foreach (var evt in response.Events)
            {
                if (evt == null) continue;

                if (!_processedEventIds.Contains(evt.EventID))
                {
                    _processedEventIds.Add(evt.EventID);
                    _allEvents.Add(evt);
                    OnNewEvent?.Invoke(evt);
                    newEventsCount++;
                }
            }
        }

        public GameEvent GetEvent(string eventId)
        {
            return _allEvents.Find(e => e.EventID == eventId);
        }

        public List<GameEvent> GetAllEvents()
        {
            return new List<GameEvent>(_allEvents);
        }

        public List<GameEvent> GetRecentEvents(int count)
        {
            if (count <= 0)
                return new List<GameEvent>();

            if (_allEvents.Count <= count)
                return new List<GameEvent>(_allEvents);

            return _allEvents.GetRange(_allEvents.Count - count, count);
        }

        public List<GameEvent> GetEventsByType(string type)
        {
            return _allEvents.FindAll(e => e.Type == type);
        }

        public void Clear()
        {
            _processedEventIds.Clear();
            _allEvents.Clear();
        }
    }
}
