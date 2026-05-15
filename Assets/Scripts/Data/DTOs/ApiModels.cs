using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommandP.Data.DTOs
{
    #region ========== 场景总览 ==========

    [System.Serializable]
    public class ScenarioOverviewResponse
    {
        public ScenarioInfo Scenario;
        public List<SideOverview> Sides;
        public string GeneratedAt;

        public ScenarioOverviewResponse()
        {
            Sides = new List<SideOverview>();
        }
    }

    [System.Serializable]
    public class ScenarioInfo
    {
        public string Title;
        public string Description;
        public string Time;
        public int TimeCompression;
        public int? Duration;
    }

    [System.Serializable]
    public class SideOverview
    {
        public string Name;
        public int TotalUnits;
        public int OperativeUnits;
        public int DestroyedUnits;
        public Dictionary<string, int> UnitTypeCounts;
        public List<GroupOverview> Groups;
        public List<UnitDetail> UngroupedUnits;

        public SideOverview()
        {
            Groups = new List<GroupOverview>();
            UngroupedUnits = new List<UnitDetail>();
            UnitTypeCounts = new Dictionary<string, int>();
        }
    }

    [System.Serializable]
    public class GroupOverview
    {
        public string GroupID;
        public string GroupName;
        public string GroupType;
        public UnitReference GroupLead;
        public List<UnitReference> Members;
        public int TotalMembers;
        public List<UnitDetail> Units;

        public GroupOverview()
        {
            Members = new List<UnitReference>();
            Units = new List<UnitDetail>();
        }
    }

    [System.Serializable]
    public class UnitReference
    {
        public string ObjectID;
        public string Name;
        public string Type;
    }

    #endregion

    #region ========== 单位详情 ==========

    [System.Serializable]
    public class UnitDetail
    {
        public string ObjectID;
        public string Name;
        public string Type;
        public int DBID;
        public GroupInfo GroupInfo;
        public UnitRealtimeState RealtimeState;
        public List<FuelDetail> Fuel;
        public List<SensorDetail> Sensors;
        public List<MountDetail> Mounts;
        public List<MagazineDetail> Magazines;
        public List<CommDetail> Comms;
        public List<PropulsionDetail> Propulsion;
        public List<SignatureDetail> Signatures;
        public DatabaseDetail DatabaseInfo;
    }

    [System.Serializable]
    public class GroupInfo
    {
        public string ParentGroupID;
        public string ParentGroupName;
        public string ParentGroupType;
        public bool IsGroupLead;
    }

    [System.Serializable]
    public class UnitRealtimeState
    {
        public Position Position;
        public Movement Movement;
        public int? Throttle;
        public UnitStatus Status;
        public MissionState MissionState;
    }

    [System.Serializable]
    public class Position
    {
        public float Latitude;
        public float Longitude;
        public float Altitude;
        public float? Depth;
    }

    [System.Serializable]
    public class Movement
    {
        public float Speed;
        public float Heading;
        public float DesiredSpeed;
        public float DesiredHeading;
    }

    [System.Serializable]
    public class UnitStatus
    {
        public string Primary;
        public string Fuel;
        public string Weapon;
        public bool IsOperative;
        public bool IsDestroyed;
        public string DamageLevel;
        public int DamagePts;
        public int InitialDP;
    }

    [System.Serializable]
    public class MissionState
    {
        public string CurrentMission;
        public string MissionPhase;
        public float? TimeOnStation;
    }

    #endregion

    #region ========== 装备详情 ==========

    [System.Serializable]
    public class FuelDetail
    {
        public string Type;
        public string TypeName;
        public double CurrentQuantity;
        public double MaxQuantity;
        public float Percentage;
        public string Unit;
    }

    [System.Serializable]
    public class SensorDetail
    {
        public string SensorID;
        public string Name;
        public int DBID;
        public string Type;
        public string TypeDescription;
        public string Role;
        public string RoleDescription;
        public float MaxRange;
        public bool IsActive;
        public SensorCapabilities Capabilities;
        public SensorSpecialCapabilities SpecialCapabilities;
        public string TechGeneration;
    }

    [System.Serializable]
    public class SensorCapabilities
    {
        public bool AirSearch;
        public bool SurfaceSearch;
        public bool SubSearch;
        public bool LandSearchMobile;
        public bool LandSearchFixed;
        public bool RangeInfo;
        public bool HeadingInfo;
        public bool AltitudeInfo;
        public bool SpeedInfo;
    }

    [System.Serializable]
    public class SensorSpecialCapabilities
    {
        public bool IFFCapable;
        public string Classification;
        public bool NCTRJEM;
        public bool NCTRNBILST;
        public bool ContinuousTracking;
        public bool TWS;
        public bool MTI;
        public bool LPI;
        public bool VisualNightCapable;
        public bool DopplerLDSDLimited;
        public bool DopplerLSDFull;
        public bool CWI;
        public bool ICWI;
        public bool GeneratesAAWFireControl;
    }

    [System.Serializable]
    public class MountDetail
    {
        public string MountID;
        public string Name;
        public int DBID;
        public int MaxCapacity;
        public int CurrentCapacity;
        public float ROF;
        public int ArmorRating;
        public List<WeaponRecDetail> Weapons;

        public MountDetail()
        {
            Weapons = new List<WeaponRecDetail>();
        }
    }

    [System.Serializable]
    public class WeaponRecDetail
    {
        public string WeaponRecID;
        public string Name;
        public int DBID;
        public int CurrentLoad;
        public int MaxLoad;
        public int DefaultLoad;
        public float PercentRemaining;
        public string Status;
    }

    [System.Serializable]
    public class MagazineDetail
    {
        public string Name;
        public int Capacity;
        public List<MagazineWeapon> Weapons;

        public MagazineDetail()
        {
            Weapons = new List<MagazineWeapon>();
        }
    }

    [System.Serializable]
    public class MagazineWeapon
    {
        public string Name;
        public int CurrentLoad;
        public int MaxLoad;
    }

    [System.Serializable]
    public class CommDetail
    {
        public string Name;
        public string Type;
        public float Range;
        public int MaxChannels;
        public CommFlags Flags;
    }

    [System.Serializable]
    public class CommFlags
    {
        public bool Broadcast;
        public bool Secure;
        public bool ReceiveOnly;
        public bool SendOnly;
    }

    [System.Serializable]
    public class PropulsionDetail
    {
        public string Name;
        public int DBID;
        public string Type;
        public int Count;
        public int MaxSpeed;
    }

    [System.Serializable]
    public class SignatureDetail
    {
        public string Type;
        public float Front;
        public float Side;
        public float Rear;
    }

    [System.Serializable]
    public class DatabaseDetail
    {
        public int DBID;
        public string UnitTypeDescription;
        public string Comments;
        public List<string> FlagDescriptions;
        public List<StoreDetail> Stores;
        public List<LoadoutDetail> Loadouts;
        public List<WarheadDetail> Warheads;

        public DatabaseDetail()
        {
            FlagDescriptions = new List<string>();
            Stores = new List<StoreDetail>();
            Loadouts = new List<LoadoutDetail>();
            Warheads = new List<WarheadDetail>();
        }
    }

    [System.Serializable]
    public class StoreDetail
    {
        public string Name;
        public int DBID;
        public float MinLaunchSpeed;
        public float MaxLaunchSpeed;
        public float MinLaunchAltAGL;
        public float MaxLaunchAltAGL;
    }

    [System.Serializable]
    public class LoadoutDetail
    {
        public string Name;
        public string Role;
    }

    [System.Serializable]
    public class WarheadDetail
    {
        public string Name;
        public string Type;
        public int DP;
    }

    #endregion

    #region ========== 事件 ==========

    [System.Serializable]
    public class EventsResponse
    {
        public List<GameEvent> Events;
        public Dictionary<string, int> ByType;
        public EventTimeRange TimeRange;

        public EventsResponse()
        {
            Events = new List<GameEvent>();
            ByType = new Dictionary<string, int>();
        }
    }

    [System.Serializable]
    public class GameEvent
    {
        public string EventID;
        public string Type;
        public string Timestamp;
        public string Description;
        public string Severity;
    }

    [System.Serializable]
    public class EventTimeRange
    {
        public string Start;
        public string End;
    }

    #endregion

    #region ========== 缓存用运行时数据 ==========

    [System.Serializable]
    public class CachedUnit
    {
        public string ObjectID;
        public string Name;
        public string Type;
        public int DBID;
        public Position Position;
        public Movement Movement;
        public UnitStatus Status;
        public List<SensorDetail> Sensors;
        public float MaxSensorRangeNm;
        public bool HasPositionChanged;
        public bool HasStatusChanged;
        public int SideID;

        [System.NonSerialized]
        public Vector3 ScenePosition;

        [System.NonSerialized]
        public Vector3 TargetScenePosition;
    }

    #endregion
}
