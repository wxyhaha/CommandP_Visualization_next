using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using CommandP.Data.DTOs;

namespace CommandP.Core
{
    public class HttpPollingService : MonoBehaviour
    {
        [SerializeField] private string _backendUrl = "http://localhost:8080";
        [SerializeField] private float _pollInterval = 3f;

        private bool _isPolling = false;
        private float _timeSinceLastPoll = 0f;
        private int _pollCount = 0;

        public System.Action<ScenarioOverviewResponse> OnOverviewReceived;
        public System.Action<EventsResponse> OnEventsReceived;
        public System.Action<string> OnError;
        public System.Action<int> OnPollCycleComplete;

        public bool IsPolling => _isPolling;
        public int PollCount => _pollCount;
        public float TimeSinceLastPoll => _timeSinceLastPoll;

        void Update()
        {
            if (!_isPolling) return;

            _timeSinceLastPoll += Time.deltaTime;
            if (_timeSinceLastPoll >= _pollInterval)
            {
                _timeSinceLastPoll = 0f;
                StartCoroutine(PollCycle());
            }
        }

        public void StartPolling()
        {
            if (_isPolling) return;
            _isPolling = true;
            _pollCount = 0;
            _timeSinceLastPoll = 0f;
            StartCoroutine(PollCycle());
        }

        public void StopPolling()
        {
            _isPolling = false;
        }

        private IEnumerator PollCycle()
        {
            yield return StartCoroutine(FetchOverview());
            yield return StartCoroutine(FetchEvents());

            _pollCount++;
            OnPollCycleComplete?.Invoke(_pollCount);
        }

        private IEnumerator FetchOverview()
        {
            string url = $"{_backendUrl}/api/overview";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        ScenarioOverviewResponse overview = JsonUtility.FromJson<ScenarioOverviewResponse>(json);

                        if (overview != null)
                        {
                            OnOverviewReceived?.Invoke(overview);
                            int sideCount = overview.Sides != null ? overview.Sides.Count : 0;
                            int unitCount = 0;
                            if (overview.Sides != null)
                            {
                                foreach (var side in overview.Sides)
                                {
                                    if (side == null) continue;
                                    unitCount += side.TotalUnits;
                                }
                            }

                            Debug.Log($"[HttpPollingService] Units present: {unitCount > 0}, count={unitCount}, sides={sideCount}");
                        }
                        else
                        {
                            OnError?.Invoke("Overview parsing returned null");
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke($"Overview parse error: {ex.Message}");
                    }
                }
                else
                {
                    OnError?.Invoke($"Overview HTTP failed: {request.responseCode} - {request.error}");
                }
            }
        }

        private IEnumerator FetchEvents()
        {
            string url = $"{_backendUrl}/api/events";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        RawEventsResponse raw = JsonUtility.FromJson<RawEventsResponse>(json);

                        var mappedEvents = new System.Collections.Generic.List<GameEvent>();
                        if (raw != null && raw.events != null)
                        {
                            foreach (var rawEvent in raw.events)
                            {
                                if (rawEvent == null) continue;

                                mappedEvents.Add(new GameEvent
                                {
                                    EventID = rawEvent.eventID,
                                    Type = rawEvent.type,
                                    Timestamp = rawEvent.timestamp,
                                    Description = rawEvent.description,
                                    Severity = rawEvent.severity
                                });
                            }
                        }

                        EventsResponse events = new EventsResponse
                        {
                            Events = mappedEvents,
                            ByType = new System.Collections.Generic.Dictionary<string, int>(),
                            TimeRange = raw != null && raw.timeRange != null
                                ? new EventTimeRange
                                {
                                    Start = raw.timeRange.start,
                                    End = raw.timeRange.end
                                }
                                : null
                        };

                        if (events != null)
                        {
                            OnEventsReceived?.Invoke(events);
                        }
                        else
                        {
                            OnError?.Invoke("Events parsing returned null");
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke($"Events parse error: {ex.Message}");
                    }
                }
                else
                {
                    OnError?.Invoke($"Events HTTP failed: {request.responseCode} - {request.error}");
                }
            }
        }

        public void SetBackendUrl(string url) => _backendUrl = url;
        public void SetPollInterval(float seconds) => _pollInterval = seconds;

        [Serializable]
        private class RawEventsResponse
        {
            public System.Collections.Generic.List<RawEventDto> events;
            public int totalCount;
            public RawEventTimeRange timeRange;
        }

        [Serializable]
        private class RawEventDto
        {
            public string eventID;
            public string type;
            public string timestamp;
            public string description;
            public string severity;
        }

        [Serializable]
        private class RawEventTimeRange
        {
            public string start;
            public string end;
        }
    }
}
