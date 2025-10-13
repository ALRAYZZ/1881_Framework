using System;
using CitizenFX.Core;

namespace VehicleManager.Server.Services
{
	/// <summary>
	/// Manages vehicle state updates and persistence
	/// </summary>
	public class WorldVehicleStateManager
	{
		private readonly WorldVehicleTracker _tracker;
		private readonly WorldVehicleRepository _repository;

		public WorldVehicleStateManager(WorldVehicleTracker tracker, WorldVehicleRepository repository)
		{
			_tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
			_repository = repository ?? throw new ArgumentNullException(nameof(repository));
		}

		public void UpdateEngineState(int vehicleNetId, bool engineOn)
		{
			if (!_tracker.TryGetDbIdFromNetId(vehicleNetId, out int dbId))
			{
				Debug.WriteLine($"[WorldVehicleStateManager] Engine state update ignored - NetID {vehicleNetId} not tracked");
				return;
			}

			// Update in-memory state
			_tracker.UpdateEngineState(dbId, engineOn);

			// Persist to database
			_repository.UpdateEngineState(dbId, engineOn);

			// Broadcast to all clients
			BaseScript.TriggerClientEvent("VehicleManager:Client:SetWorldVehicleEngineState", vehicleNetId, engineOn);

			Debug.WriteLine($"[WorldVehicleStateManager] Updated engine_on={engineOn} for DB ID {dbId} (NetID {vehicleNetId})");
		}

		public void UpdateVehiclePosition(int dbId, float x, float y, float z, float heading,
			float rx, float ry, float rz, int primaryColor, int secondaryColor,
			string customPrimaryRGB, string customSecondaryRGB)
		{
			// Update in-memory tracking
			_tracker.UpdateVehiclePosition(dbId, x, y, z, heading, primaryColor, secondaryColor,
				customPrimaryRGB, customSecondaryRGB);

			// Persist to database
			_repository.UpdateVehiclePosition(dbId, x, y, z, heading, rx, ry, rz,
				primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);

			Debug.WriteLine($"[WorldVehicleStateManager] Updated position and colors for vehicle DB ID {dbId}");
		}

		public void SyncWorldVehiclesToPlayer(Player player)
		{
			if (player == null) return;

			int count = _tracker.TrackedVehicleCount;
			Debug.WriteLine($"[WorldVehicleStateManager] Syncing {count} world vehicles for player {player.Name}");

			foreach (var kvp in _tracker.GetAllVehicles())
			{
				int dbId = kvp.Key;
				var vehicleData = kvp.Value;
				int netId = vehicleData.NetId;

				if (netId != 0)
				{
					player.TriggerEvent("VehicleManager:Client:RegisterWorldVehicle", netId, dbId);
					player.TriggerEvent("VehicleManager:Client:SetWorldVehicleEngineState", netId, vehicleData.EngineOn);
				}
			}
		}
	}
}