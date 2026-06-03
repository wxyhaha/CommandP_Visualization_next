using System.Collections.Generic;

namespace CommandP.GlobalEntity.Data
{
    /// <summary>
    /// Test data factory — South China Sea scenario.
    /// Aircraft now use waypoint-based spline flight paths.
    /// Ships / Missiles / Ground vehicles keep straight-line motion.
    /// </summary>
    public static class TestEntityDataFactory
    {
        public static EntityData[] CreateSouthChinaSeaScenario()
        {
            var list = new List<EntityData>();

            // ==================== Ships (Surface & Submarine) ====================

            list.Add(CreateShip("SHIP_DDG_01", "Type 055 Destroyer (Red)",
                11.5, 114.3, 0.0, 15f, 180f,
                "bengaluru_class_destroyer_d67", 1));

            list.Add(CreateShip("SHIP_DDG_02", "Type 052D Destroyer (Red)",
                11.6, 114.5, 0.0, 18f, 165f,
                "bengaluru_class_destroyer_d67", 1));

            list.Add(CreateShip("SHIP_FFG_01", "Type 054A Frigate (Red)",
                11.3, 113.9, 0.0, 20f, 200f,
                "bengaluru_class_destroyer_d67", 1));

            list.Add(CreateShip("SHIP_FFG_02", "Type 054A Frigate (Red)",
                11.7, 114.1, 0.0, 20f, 190f,
                "bengaluru_class_destroyer_d67", 1));

            list.Add(CreateShip("SHIP_AOE_01", "Type 901 Replenishment (Red)",
                11.4, 114.6, 0.0, 12f, 150f,
                "bengaluru_class_destroyer_d67", 1));

            var sub = CreateShip("SHIP_SUB_01", "Type 093 SSN (Red)",
                12.0, 115.5, -50.0, 10f, 100f,
                "the_project_941__akula__typhoon_submarine", 1);
            sub.IconKey = "submarine";
            list.Add(sub);

            list.Add(CreateShip("SHIP_DDG_03", "Arleigh Burke DDG (Blue)",
                10.8, 114.0, 0.0, 20f, 0f,
                "bengaluru_class_destroyer_d67", 2));

            list.Add(CreateShip("SHIP_DDG_04", "Arleigh Burke DDG (Blue)",
                10.6, 114.2, 0.0, 20f, 30f,
                "bengaluru_class_destroyer_d67", 2));

            // ==================== Aircraft (spline flight paths) ====================

            // E-2D Hawkeye: slow patrol diamond around 12.5N 115E
            list.Add(CreateAircraft("AC_PATROL_01", "E-2D Hawkeye (Red)",
                300f, "fa-18f", 1,
                (12.5, 115.0, 8000.0),
                (13.0, 115.5, 8000.0),
                (12.5, 116.0, 8000.0),
                (12.0, 115.5, 8000.0)));

            // F/A-18F #1: sweep from west through center
            list.Add(CreateAircraft("AC_FIGHTER_01", "F/A-18F (Red)",
                500f, "fa-18f", 1,
                (11.0, 113.5, 10000.0),
                (11.8, 114.5, 10000.0),
                (12.5, 115.5, 10000.0),
                (12.0, 116.0, 10000.0),
                (11.2, 115.0, 10000.0),
                (11.0, 113.5, 10000.0)));

            // F/A-18F #2: close air support orbit near fleet
            list.Add(CreateAircraft("AC_FIGHTER_02", "F/A-18F (Red)",
                520f, "fa-18f", 1,
                (11.2, 113.8, 10000.0),
                (11.5, 114.2, 10000.0),
                (11.3, 114.6, 10000.0),
                (11.0, 114.2, 10000.0),
                (11.2, 113.8, 10000.0)));

            // MiG-29 #3: patrol from northeast
            list.Add(CreateAircraft("AC_FIGHTER_03", "MiG-29 (Blue)",
                450f, "mig29", 2,
                (13.0, 115.5, 9000.0),
                (13.5, 116.0, 9000.0),
                (13.0, 116.5, 9000.0),
                (12.5, 116.0, 9000.0),
                (13.0, 115.5, 9000.0)));

            // MiG-29 #4: intercept heading southwest
            list.Add(CreateAircraft("AC_FIGHTER_04", "MiG-29 (Blue)",
                460f, "mig29", 2,
                (13.2, 115.3, 9000.0),
                (12.8, 115.0, 9000.0),
                (12.3, 114.8, 9000.0),
                (12.8, 115.2, 9000.0),
                (13.2, 115.3, 9000.0)));

            // ==================== LEO Satellites ====================

            list.Add(CreateSatellite("SAT_LEO_01", "LEO Recon Satellite 1",
                250.0, 45.0, 0.0, 0.0, 1));

            list.Add(CreateSatellite("SAT_LEO_02", "LEO Recon Satellite 2",
                350.0, 55.0, 30.0, 120.0, 1));

            // ==================== Missiles ====================

            list.Add(CreateMissile("MSL_ASM_01", "YJ-18 ASCM (Red)",
                11.5, 114.4, 30.0, 600f, 135f,
                "ugm-84", 1));

            list.Add(CreateMissile("MSL_ASM_02", "YJ-18 ASCM (Red)",
                11.3, 114.2, 30.0, 620f, 140f,
                "ugm-84", 1));

            // ==================== Ground Vehicles ====================

            list.Add(CreateGroundVehicle("GV_SAM_01", "HQ-9 SAM Battery (Red)",
                11.0, 110.0, 50.0, 0f, 0f,
                "mim-104", 1));

            list.Add(CreateGroundVehicle("GV_RADAR_01", "Type 120 Radar (Red)",
                11.1, 110.1, 100.0, 0f, 0f,
                "mim-104", 1));

            return list.ToArray();
        }

        // ================================================================
        // Aircraft with waypoints (spline flight)
        // ================================================================

        private static EntityData CreateAircraft(
            string id, string name,
            float speedKts, string modelKey, int sideId,
            params (double lat, double lon, double alt)[] waypoints)
        {
            var wpList = new List<double[]>();
            foreach (var w in waypoints)
                wpList.Add(new double[] { w.lat, w.lon, w.alt });

            // Initial position = first waypoint
            var first = wpList[0];

            var entity = new EntityData
            {
                ObjectId = id,
                DisplayName = name,
                Type = EntityType.Aircraft,
                SideId = sideId,
                LatitudeDeg = first[0],
                LongitudeDeg = first[1],
                HeightMeters = first[2],
                SpeedKnots = speedKts,
                HeadingDeg = 0f,
                ModelAssetKey = modelKey,
                EcefDirty = true,
                Waypoints = wpList,
                CurrentSegmentIndex = 0,
                SegmentProgress = 0.0,
            };
            return entity;
        }

        // ================================================================
        // Ships (straight-line)
        // ================================================================

        private static EntityData CreateShip(
            string id, string name,
            double lat, double lon, double alt,
            float speedKts, float heading,
            string modelKey, int sideId)
        {
            return new EntityData
            {
                ObjectId = id,
                DisplayName = name,
                Type = EntityType.Ship,
                SideId = sideId,
                LatitudeDeg = lat,
                LongitudeDeg = lon,
                HeightMeters = alt,
                SpeedKnots = speedKts,
                HeadingDeg = heading,
                ModelAssetKey = modelKey,
                EcefDirty = true,
            };
        }

        // ================================================================
        // Satellites (orbital)
        // ================================================================

        private static EntityData CreateSatellite(
            string id, string name,
            double altitudeKm, double inclinationDeg,
            double raanDeg, double phaseDeg, int sideId)
        {
            return new EntityData
            {
                ObjectId = id,
                DisplayName = name,
                Type = EntityType.Satellite,
                SideId = sideId,
                LatitudeDeg = 12.0,
                LongitudeDeg = 115.0,
                HeightMeters = altitudeKm * 1000.0,
                SpeedKnots = 17000f,
                HeadingDeg = 90f,
                ModelAssetKey = "satellite",
                HasOrbitParams = true,
                OrbitAltitudeKm = altitudeKm,
                OrbitInclinationDeg = inclinationDeg,
                OrbitRaanDeg = raanDeg,
                OrbitPhaseDeg = phaseDeg,
                EcefDirty = true,
            };
        }

        // ================================================================
        // Missiles (straight-line)
        // ================================================================

        private static EntityData CreateMissile(
            string id, string name,
            double lat, double lon, double alt,
            float speedKts, float heading,
            string modelKey, int sideId)
        {
            return new EntityData
            {
                ObjectId = id,
                DisplayName = name,
                Type = EntityType.Missile,
                SideId = sideId,
                LatitudeDeg = lat,
                LongitudeDeg = lon,
                HeightMeters = alt,
                SpeedKnots = speedKts,
                HeadingDeg = heading,
                ModelAssetKey = modelKey,
                EcefDirty = true,
            };
        }

        // ================================================================
        // Ground Vehicles (stationary)
        // ================================================================

        private static EntityData CreateGroundVehicle(
            string id, string name,
            double lat, double lon, double alt,
            float speedKts, float heading,
            string modelKey, int sideId)
        {
            return new EntityData
            {
                ObjectId = id,
                DisplayName = name,
                Type = EntityType.GroundVehicle,
                SideId = sideId,
                LatitudeDeg = lat,
                LongitudeDeg = lon,
                HeightMeters = alt,
                SpeedKnots = speedKts,
                HeadingDeg = heading,
                ModelAssetKey = modelKey,
                EcefDirty = true,
            };
        }
    }
}