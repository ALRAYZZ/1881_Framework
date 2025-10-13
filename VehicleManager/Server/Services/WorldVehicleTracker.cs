using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using VehicleManager.Server.Models;

namespace VehicleManager.Server.Services
{
	/// <summary>
	/// Manages runtime tracking of world vehicles
	/// </summary>
	public class WorldVehicleTracker
	{
		private readonly Dictionary<int, WorldVehicleData> _worldVehiclesByDbId = new Dictionary<int, WorldVehicleData>();
		private readonly Dictionary<int, int> _netIdToDbId = new Dictionary<int, int>();
		private readonly HashSet<int> _ignoredRemovedEntities = new HashSet<int>();

		public int TrackedVehicleCount => _worldVehiclesByDbId.Count;

		public void TrackVehicle(int dbId, WorldVehicleData vehicleData)
		{
			_worldVehiclesByDbId[dbId] = vehicleData;

			if (vehicleData.NetId != 0)
			{
				_netIdToDbId[vehicleData.NetId] = dbId;
				Debug.WriteLine($"[WorldVehicleTracker] Tracking vehicle - DB ID: {dbId}, NetID: {vehicleData.NetId}");
			}
		}

		public void UntrackVehicle(int dbId)
		{
			if (_worldVehiclesByDbId.TryGetValue(dbId, out var vehicleData))
			{
				if (vehicleData.NetId != 0)
				{
					_netIdToDbId.Remove(vehicleData.NetId);
				}
				_worldVehiclesByDbId.Remove(dbId);
				Debug.WriteLine($"[WorldVehicleTracker] Untracked vehicle DB ID: {dbId}");
			}
		}

		public bool TryGetVehicleByNetId(int netId, out int dbId, out WorldVehicleData vehicleData)
		{
			if (_netIdToDbId.TryGetValue(netId, out dbId))
			{
				return _worldVehiclesByDbId.TryGetValue(dbId, out vehicleData);
			}

			dbId = 0;
			vehicleData = null;
			return false;
		}

		public bool TryGetVehicleByDbId(int dbId, out WorldVehicleData vehicleData)
		{
			return _worldVehiclesByDbId.TryGetValue(dbId, out vehicleData);
		}

		public bool IsWorldVehicle(int netId)
		{
			return _netIdToDbId.ContainsKey(netId);
		}

		public bool TryGetDbIdFromNetId(int netId, out int dbId)
		{
			return _netIdToDbId.TryGetValue(netId, out dbId);
		}

		public WorldVehicleData FindByModelAndPlate(uint modelHash, string plate)
		{
			return _worldVehiclesByDbId.Values.FirstOrDefault(v =>
				v.ModelHash == modelHash &&
				string.Equals(v.Plate?.Trim(), plate?.Trim(), System.StringComparison.OrdinalIgnoreCase));
		}

		public void UpdateVehiclePosition(int dbId, float x, float y, float z, float heading,
			int primaryColor, int secondaryColor, string customPrimaryRGB, string customSecondaryRGB)
		{
			if (_worldVehiclesByDbId.TryGetValue(dbId, out var vehicle))
			{
				vehicle.X = x;
				vehicle.Y = y;
				vehicle.Z = z;
				vehicle.Heading = heading;
				vehicle.PrimaryColor = primaryColor;
				vehicle.SecondaryColor = secondaryColor;
				vehicle.CustomPrimaryRGB = customPrimaryRGB;
				vehicle.CustomSecondaryRGB = customSecondaryRGB;
			}
		}

		public void UpdateEngineState(int dbId, bool engineOn)
		{
			if (_worldVehiclesByDbId.TryGetValue(dbId, out var vehicle))
			{
				vehicle.EngineOn = engineOn;
			}
		}

		public void UpdateNetworkId(int dbId, int newNetId, int newEntityId)
		{
			if (_worldVehiclesByDbId.TryGetValue(dbId, out var vehicle))
			{
				// Remove old NetId mapping
				if (vehicle.NetId != 0)
				{
					_netIdToDbId.Remove(vehicle.NetId);
				}

				// Update to new values
				vehicle.NetId = newNetId;
				vehicle.EntityId = newEntityId;

				// Add new NetId mapping
				if (newNetId != 0)
				{
					_netIdToDbId[newNetId] = dbId;
				}
			}
		}

		public List<KeyValuePair<int, WorldVehicleData>> GetAllVehicles()
		{
			return _worldVehiclesByDbId.ToList();
		}

		public void MarkEntityAsIntentionallyDeleted(int entityId)
		{
			_ignoredRemovedEntities.Add(entityId);
		}

		public bool ShouldIgnoreEntityRemoval(int entityId)
		{
			return _ignoredRemovedEntities.Remove(entityId);
		}

		public void LogTrackingState()
		{
			Debug.WriteLine($"[WorldVehicleTracker] Tracking {_worldVehiclesByDbId.Count} vehicles");
			Debug.WriteLine($"[WorldVehicleTracker] Network ID mappings: {_netIdToDbId.Count}");

			foreach (var kvp in _netIdToDbId)
			{
				Debug.WriteLine($"[WorldVehicleTracker]   NetID {kvp.Key} => DB ID {kvp.Value}");
			}
		}
	}
}