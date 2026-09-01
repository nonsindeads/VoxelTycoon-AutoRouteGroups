using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Game.UI.ModernUI;
using VoxelTycoon.Modding;
using VoxelTycoon.Notifications;
using VoxelTycoon.Serialization;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Tasks;
using VoxelTycoon.UI;

namespace AutoRouteGroups
{
    /// <summary>
    /// Uses Voxel Tycoon's native saved routes as named vehicle groups.
    /// No Harmony patches or native code are used, so the mod remains Mac compatible.
    /// </summary>
    public sealed class AutoRouteGroupsMod : Mod
    {
        private const int SaveVersion = 1;
        private const float DebounceSeconds = 1.5f;
        private const float SafetyScanSeconds = 20f;
        private const string AutoSuffix = " A";

        private readonly HashSet<int> _managedRouteIds = new HashSet<int>();

        private bool _automatic = true;
        private bool _protectManualRoutes = true;
        private bool _dirty = true;
        private bool _organizing;
        private bool _subscribed;
        private float _nextRunAt;
        private float _nextSafetyScanAt;

        protected override void OnGameStarted()
        {
            Subscribe();
            PruneMissingManagedRoutes();

            Toolbar.Current.AddButton(
                FontIcon.FaSolid("\uf021"),
                "Auto Routes: organize now",
                new InstantToolbarAction(RunManually));

            Toolbar.Current.AddButton(
                FontIcon.FaSolid("\uf013"),
                "Auto Routes: toggle automatic mode",
                new InstantToolbarAction(ToggleAutomatic));

            Toolbar.Current.AddButton(
                FontIcon.FaSolid("\uf3ed"),
                "Auto Routes: protect/include manual routes",
                new InstantToolbarAction(ToggleManualRouteProtection));

            _dirty = true;
            _nextRunAt = Time.unscaledTime + 2f;
            _nextSafetyScanAt = Time.unscaledTime + SafetyScanSeconds;
            Debug.Log("[AutoRouteGroups] Loaded without Harmony. Automatic=" + _automatic
                + ", ProtectManualRoutes=" + _protectManualRoutes + ".");
        }

        protected override void OnUpdate()
        {
            if (!_automatic || _organizing)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _nextSafetyScanAt)
            {
                _dirty = true;
                _nextSafetyScanAt = now + SafetyScanSeconds;
            }

            if (_dirty && now >= _nextRunAt)
            {
                Organize(showNotification: false);
            }
        }

        protected override void Deinitialize()
        {
            Unsubscribe();
        }

        protected override void Read(StateBinaryReader reader)
        {
            int version = reader.ReadInt();
            _automatic = reader.ReadBool();
            _protectManualRoutes = reader.ReadBool();
            _managedRouteIds.Clear();
            foreach (int id in reader.ReadIntArray())
            {
                _managedRouteIds.Add(id);
            }

            if (version > SaveVersion)
            {
                Debug.LogWarning("[AutoRouteGroups] Save data is newer than this mod version.");
            }
        }

        protected override void Write(StateBinaryWriter writer)
        {
            writer.WriteInt(SaveVersion);
            writer.WriteBool(_automatic);
            writer.WriteBool(_protectManualRoutes);
            writer.WriteIntArray(_managedRouteIds.OrderBy(x => x).ToArray());
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            LazyManager<VehicleManager>.Current.Changed += OnVehicleChanged;
            LazyManager<VehicleRouteManager>.Current.RouteChanged += OnRouteChanged;
            LazyManager<VehicleRouteManager>.Current.RouteCreated += OnRouteCreated;
            LazyManager<VehicleRouteManager>.Current.RouteRemoved += OnRouteRemoved;
            LazyManager<VehicleRouteManager>.Current.RouteSet += OnRouteSet;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            LazyManager<VehicleManager>.Current.Changed -= OnVehicleChanged;
            LazyManager<VehicleRouteManager>.Current.RouteChanged -= OnRouteChanged;
            LazyManager<VehicleRouteManager>.Current.RouteCreated -= OnRouteCreated;
            LazyManager<VehicleRouteManager>.Current.RouteRemoved -= OnRouteRemoved;
            LazyManager<VehicleRouteManager>.Current.RouteSet -= OnRouteSet;
            _subscribed = false;
        }

        private void OnVehicleChanged(Vehicle vehicle, VehicleManagerChangedEventType eventType)
        {
            MarkDirty();
        }

        private void OnRouteChanged(Vehicle vehicle, VehicleRoute route)
        {
            MarkDirty();
        }

        private void OnRouteCreated(VehicleRoute route)
        {
            MarkDirty();
        }

        private void OnRouteRemoved(VehicleRoute route)
        {
            _managedRouteIds.Remove(route.Id);
            MarkDirty();
        }

        private void OnRouteSet(Vehicle vehicle, VehicleRoute route)
        {
            MarkDirty();
        }

        private void MarkDirty()
        {
            if (_organizing)
            {
                return;
            }

            _dirty = true;
            _nextRunAt = Time.unscaledTime + DebounceSeconds;
        }

        private void RunManually()
        {
            Organize(showNotification: true);
        }

        private void ToggleAutomatic()
        {
            _automatic = !_automatic;
            if (_automatic)
            {
                _dirty = true;
                _nextRunAt = Time.unscaledTime;
            }

            Notify(
                "Auto Routes",
                _automatic
                    ? "Automatic mode is ON. Changes are organized after a short delay."
                    : "Automatic mode is OFF. Manual organization remains available.");
        }

        private void ToggleManualRouteProtection()
        {
            _protectManualRoutes = !_protectManualRoutes;
            _dirty = true;
            _nextRunAt = Time.unscaledTime;

            Notify(
                "Auto Routes",
                _protectManualRoutes
                    ? "Manual routes are PROTECTED and will not be changed."
                    : "Manual routes are INCLUDED. Matching vehicles may be moved and matching routes adopted.");
        }

        private void Organize(bool showNotification)
        {
            if (_organizing)
            {
                return;
            }

            _organizing = true;
            _dirty = false;

            try
            {
                OrganizationResult result = OrganizeCore();
                Debug.Log("[AutoRouteGroups] " + result.ToLogString());

                if (showNotification)
                {
                    Notify("Auto Routes", result.ToUserMessage(_protectManualRoutes));
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[AutoRouteGroups] Organizing failed:\n" + exception);
                if (showNotification)
                {
                    Notify("Auto Routes", "Organization failed. See the game log for details.");
                }
            }
            finally
            {
                _organizing = false;
                _nextSafetyScanAt = Time.unscaledTime + SafetyScanSeconds;
            }
        }

        private OrganizationResult OrganizeCore()
        {
            VehicleRouteManager routeManager = LazyManager<VehicleRouteManager>.Current;
            List<Vehicle> allVehicles = LazyManager<VehicleManager>.Current.GetAll()
                .ToList()
                .Where(IsEligibleVehicle)
                .ToList();

            PruneMissingManagedRoutes();

            List<Vehicle> candidates = allVehicles
                .Where(v => !_protectManualRoutes || v.Route == null || IsManaged(v.Route))
                .ToList();

            Dictionary<string, List<Vehicle>> groups = candidates
                .GroupBy(BuildGroupingKey)
                .ToDictionary(g => g.Key, g => g.OrderBy(v => v.Id).ToList());

            Dictionary<int, string> stopAbbreviations = BuildStopAbbreviations();
            Dictionary<int, string> cargoAbbreviations = BuildCargoAbbreviations(allVehicles);

            OrganizationResult result = new OrganizationResult
            {
                EligibleVehicles = allVehicles.Count,
                ProtectedVehicles = allVehicles.Count - candidates.Count
            };

            foreach (List<Vehicle> group in groups.Values.Where(g => g.Count >= 2).OrderBy(g => g[0].Id))
            {
                VehicleRoute target = FindManagedTarget(group);
                if (target == null && !_protectManualRoutes)
                {
                    target = FindAdoptableManualTarget(group);
                    if (target != null)
                    {
                        _managedRouteIds.Add(target.Id);
                        result.AdoptedRoutes++;
                    }
                }

                string desiredName = BuildRouteName(group[0], stopAbbreviations, cargoAbbreviations);
                if (target == null)
                {
                    target = CreateRouteKeepingSchedule(group[0], desiredName);
                    _managedRouteIds.Add(target.Id);
                    result.CreatedRoutes++;
                }

                desiredName = MakeUniqueName(desiredName, target);
                if (target.Name != desiredName)
                {
                    target.Name = desiredName;
                    result.RenamedRoutes++;
                }

                foreach (Vehicle vehicle in group)
                {
                    if (vehicle.Route != target)
                    {
                        VehicleRoute.Set(vehicle, target);
                        result.MovedVehicles++;
                    }
                }

                result.ActiveGroups++;
            }

            // A route managed by this mod must never retain vehicles that no longer
            // have an identical partner. Detach those vehicles but preserve schedules.
            HashSet<Vehicle> vehiclesInValidGroups = new HashSet<Vehicle>(
                groups.Values.Where(g => g.Count >= 2).SelectMany(g => g));

            foreach (Vehicle vehicle in allVehicles.OrderBy(v => v.Id).ToList())
            {
                if (vehicle.Route != null && IsManaged(vehicle.Route) && !vehiclesInValidGroups.Contains(vehicle))
                {
                    DetachKeepingSchedule(vehicle);
                    result.DetachedSingles++;
                }
            }

            foreach (VehicleRoute route in routeManager.Routes.ToList()
                         .Where(r => IsManaged(r) && r.Vehicles.Count == 0)
                         .OrderBy(r => r.Id)
                         .ToList())
            {
                routeManager.Remove(route);
                _managedRouteIds.Remove(route.Id);
                result.RemovedEmptyRoutes++;
            }

            return result;
        }

        private static bool IsEligibleVehicle(Vehicle vehicle)
        {
            return vehicle != null
                && vehicle.IsBought
                && vehicle.Units.Count > 0
                && !vehicle.Schedule.IsEmpty;
        }

        private bool IsManaged(VehicleRoute route)
        {
            return route != null && _managedRouteIds.Contains(route.Id);
        }

        private static string BuildGroupingKey(Vehicle vehicle)
        {
            string consist = string.Join(",", vehicle.Units.ToList().Select(
                unit => unit.AssetId + (unit.Flipped ? "R" : "F")));

            return ((int)vehicle.Type) + "|" + consist + "|" + vehicle.Schedule.GetScheduleVersion();
        }

        private VehicleRoute FindManagedTarget(List<Vehicle> group)
        {
            HashSet<Vehicle> members = new HashSet<Vehicle>(group);
            return group
                .Select(v => v.Route)
                .Where(r => r != null && IsManaged(r))
                .GroupBy(r => r.Id)
                .Where(g => g.First().Vehicles.ToList().All(members.Contains))
                .OrderByDescending(g => g.First().Vehicles.Count)
                .ThenBy(g => g.Key)
                .Select(g => g.First())
                .FirstOrDefault();
        }

        private static VehicleRoute FindAdoptableManualTarget(List<Vehicle> group)
        {
            HashSet<Vehicle> members = new HashSet<Vehicle>(group);
            return group
                .Select(v => v.Route)
                .Where(r => r != null)
                .Distinct()
                .Where(r => r.Vehicles.ToList().All(members.Contains))
                .OrderByDescending(r => r.Vehicles.Count)
                .ThenBy(r => r.Id)
                .FirstOrDefault();
        }

        private VehicleRoute CreateRouteKeepingSchedule(Vehicle seed, string name)
        {
            if (seed.Route != null)
            {
                DetachKeepingSchedule(seed);
            }

            return LazyManager<VehicleRouteManager>.Current.Create(name, seed);
        }

        private static void DetachKeepingSchedule(Vehicle vehicle)
        {
            if (vehicle.Route == null)
            {
                return;
            }

            VehicleScheduleTraverseOrder traverseOrder = vehicle.Schedule.TraverseOrder;
            List<RootTask> snapshot = vehicle.Schedule.GetTasks().ToList()
                .Select(task => task.Clone(null))
                .ToList();

            VehicleRoute.Set(vehicle, null);
            vehicle.Schedule.CopyFrom(snapshot, traverseOrder);
        }

        private string BuildRouteName(
            Vehicle vehicle,
            Dictionary<int, string> stopAbbreviations,
            Dictionary<int, string> cargoAbbreviations)
        {
            List<Item> cargoItems = GetCargoItems(vehicle);
            string kind = vehicle.Type == VehicleType.Train ? "ZUG" : (cargoItems.Count > 0 ? "LKW" : "PKW");
            string cargoCode;
            if (cargoItems.Count == 0)
            {
                cargoCode = "OHN";
            }
            else if (cargoItems.Count == 1)
            {
                cargoCode = cargoAbbreviations[cargoItems[0].AssetId];
            }
            else if (cargoItems.Count == 2)
            {
                cargoCode = cargoAbbreviations[cargoItems[0].AssetId]
                    + "+" + cargoAbbreviations[cargoItems[1].AssetId];
            }
            else
            {
                cargoCode = "MIX";
            }

            List<string> parts = new List<string> { cargoCode, kind };

            List<string> stops = vehicle.Schedule.GetTasks().ToList()
                .OfType<VehicleStationTask>()
                .Select(task => task.Destination?.Location)
                .Where(location => location != null)
                .Select(location => stopAbbreviations.TryGetValue(location.Id, out string abbreviation)
                    ? abbreviation
                    : StopAbbreviator.CreateCandidate(location.Name, 4))
                .ToList();

            // Remove only adjacent duplicates; revisiting a station later remains meaningful.
            for (int i = stops.Count - 1; i > 0; i--)
            {
                if (stops[i] == stops[i - 1])
                {
                    stops.RemoveAt(i);
                }
            }

            if (stops.Count == 1)
            {
                parts.Add(stops[0]);
            }
            else if (stops.Count == 2)
            {
                parts.Add(stops[0] + "↔" + stops[1]);
            }
            else if (stops.Count == 3)
            {
                string arrow = vehicle.Schedule.TraverseOrder == VehicleScheduleTraverseOrder.BackAndForth ? "↔" : "→";
                parts.Add(string.Join(arrow, stops));
            }
            else if (stops.Count > 3)
            {
                string arrow = vehicle.Schedule.TraverseOrder == VehicleScheduleTraverseOrder.BackAndForth ? "↔" : "→";
                parts.Add(stops[0] + arrow + stops[1] + arrow + "…" + arrow + stops[stops.Count - 1]);
            }
            else
            {
                parts.Add("FP-" + Math.Abs(vehicle.Schedule.GetScheduleVersion() % 10000).ToString("0000"));
            }

            return string.Join(" ", parts) + AutoSuffix;
        }

        private static Dictionary<int, string> BuildStopAbbreviations()
        {
            return StopAbbreviator.Build(
                LazyManager<VehicleDestinationLocationManager>.Current.GetAll().ToList()
                    .Where(location => location is VehicleStationLocation && !location.IsDead)
                    .Select(location => new KeyValuePair<int, string>(location.Id, location.Name)));
        }

        private static List<Item> GetCargoItems(Vehicle vehicle)
        {
            // Explicit refit orders describe the actual route better than the list
            // of every item a wagon could theoretically carry.
            List<Item> items = vehicle.Schedule.GetTasks().ToList()
                .OfType<VehicleStationTask>()
                .SelectMany(task => task.GetSubTasks().ToList())
                .OfType<RefitTask>()
                .Select(task => task.Item)
                .Where(item => item != null)
                .ToList();

            if (items.Count == 0)
            {
                items = vehicle.Units.ToList()
                    .Select(unit => unit.Storage?.Item)
                    .Where(item => item != null)
                    .ToList();
            }

            if (items.Count == 0)
            {
                items = vehicle.Items.ToList();
            }

            return items
                .GroupBy(item => item.AssetId)
                .Select(group => group.First())
                .OrderBy(item => item.DisplayName?.ToString())
                .ToList();
        }

        private static Dictionary<int, string> BuildCargoAbbreviations(IEnumerable<Vehicle> vehicles)
        {
            return StopAbbreviator.Build(
                vehicles
                    .SelectMany(GetCargoItems)
                    .GroupBy(item => item.AssetId)
                    .Select(group => group.First())
                    .Select(item => new KeyValuePair<int, string>(item.AssetId, item.DisplayName?.ToString() ?? "Cargo")),
                minimumLength: 3,
                maximumLength: 4);
        }

        private string MakeUniqueName(string desiredName, VehicleRoute target)
        {
            List<VehicleRoute> collisions = LazyManager<VehicleRouteManager>.Current.Routes.ToList()
                .Where(r => r != target && string.Equals(r.Name, desiredName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (collisions.Count == 0)
            {
                return desiredName;
            }

            Vehicle representative = target?.Vehicles.FirstOrDefault();
            string model = representative?.Units.FirstOrDefault()?.SharedData?.DisplayName?.ToString();
            if (!string.IsNullOrWhiteSpace(model))
            {
                string withModel = InsertBeforeAutoSuffix(desiredName, Shorten(model, 6));
                if (!LazyManager<VehicleRouteManager>.Current.Routes.ToList().Any(
                        r => r != target && string.Equals(r.Name, withModel, StringComparison.OrdinalIgnoreCase)))
                {
                    return withModel;
                }
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = InsertBeforeAutoSuffix(desiredName, (suffix++).ToString());
            }
            while (LazyManager<VehicleRouteManager>.Current.Routes.ToList().Any(
                r => r != target && string.Equals(r.Name, candidate, StringComparison.OrdinalIgnoreCase)));

            return candidate;
        }

        private static string InsertBeforeAutoSuffix(string routeName, string value)
        {
            if (routeName.EndsWith(AutoSuffix, StringComparison.Ordinal))
            {
                return routeName.Substring(0, routeName.Length - AutoSuffix.Length)
                    + " " + value + AutoSuffix;
            }

            return routeName + " " + value;
        }

        private static string Shorten(string value, int maxLength)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, Math.Max(1, maxLength - 1)).TrimEnd() + "…";
        }

        private void PruneMissingManagedRoutes()
        {
            HashSet<int> existingIds = new HashSet<int>(
                LazyManager<VehicleRouteManager>.Current.Routes.ToList().Select(r => r.Id));
            _managedRouteIds.RemoveWhere(id => !existingIds.Contains(id));
        }

        private static void Notify(string title, string message)
        {
            Manager<NotificationManager>.Current.Push(title, message, null);
        }

        private sealed class OrganizationResult
        {
            public int ActiveGroups;
            public int AdoptedRoutes;
            public int CreatedRoutes;
            public int DetachedSingles;
            public int EligibleVehicles;
            public int MovedVehicles;
            public int ProtectedVehicles;
            public int RemovedEmptyRoutes;
            public int RenamedRoutes;

            public string ToUserMessage(bool protectsManualRoutes)
            {
                string message = ActiveGroups + " groups checked, "
                    + CreatedRoutes + " routes created, "
                    + MovedVehicles + " vehicles assigned";

                if (AdoptedRoutes > 0)
                {
                    message += ", " + AdoptedRoutes + " existing routes adopted";
                }

                if (DetachedSingles > 0)
                {
                    message += ", " + DetachedSingles + " single vehicles detached";
                }

                if (protectsManualRoutes && ProtectedVehicles > 0)
                {
                    message += ". " + ProtectedVehicles + " vehicles in manual routes remained protected";
                }

                return message + ".";
            }

            public string ToLogString()
            {
                return "Eligible=" + EligibleVehicles
                    + ", Protected=" + ProtectedVehicles
                    + ", Groups=" + ActiveGroups
                    + ", Created=" + CreatedRoutes
                    + ", Adopted=" + AdoptedRoutes
                    + ", Renamed=" + RenamedRoutes
                    + ", Moved=" + MovedVehicles
                    + ", Detached=" + DetachedSingles
                    + ", RemovedEmpty=" + RemovedEmptyRoutes;
            }
        }
    }
}
