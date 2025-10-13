using System;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using VehicleManager.Server.Models;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Server.Services
{
	/// <summary>
	/// Monitors world vehicles and handles respawning
	/// </summary>
	public class WorldVehicleMonitor : BaseScript
	{
		private readonly WorldVehicleTracker _tracker;
		private readonly WorldVehicleSpawner _spawner;
		private const int MONITOR_INTERVAL_MS = 30000;
		private const int RESPAWN_DELAY_MS = 5000;

		public WorldVehicleMonitor(WorldVehicleTracker tracker, WorldVehicleSpawner spawner)
		{
			_tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
			_spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
		}

		public void StartMonitoring()
		{
			Tick += MonitorVehiclesTick;
			Debug.WriteLine("[WorldVehicleMonitor] Started monitoring world vehicles");
		}

		public void HandleEntityRemoval(int entity)
		{
			try
			{
				if (_tracker.ShouldIgnoreEntityRemoval(entity))
				{
					Debug.WriteLine($"[WorldVehicleMonitor] Ignoring removal of intentionally deleted entity {entity}");
					return;
				}

				int netId = NetworkGetNetworkIdFromEntity(entity);
				if (netId != 0 && _tracker.TryGetVehicleByNetId(netId, out int dbId, out WorldVehicleData vehicleData))
				{
					Debug.WriteLine($"[WorldVehicleMonitor] World vehicle destroyed (DB ID: {dbId}, Entity: {entity}). Scheduling respawn...");
					_ = RespawnVehicleAsync(dbId, vehicleData);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[WorldVehicleMonitor] Error in HandleEntityRemoval: {ex.Message}");
			}
		}

		private async Task MonitorVehiclesTick()
		{
			await Delay(MONITOR_INTERVAL_MS);

			Debug.WriteLine($"[WorldVehicleMonitor] Checking {_tracker.TrackedVehicleCount} vehicles...");

			foreach (var kvp in _tracker.GetAllVehicles())
			{
				try
				{
					int dbId = kvp.Key;
					var data = kvp.Value;
					int entity = data.EntityId;

					if (entity == 0 || !DoesEntityExist(entity))
					{
						Debug.WriteLine($"[WorldVehicleMonitor] Vehicle {dbId} missing, scheduling respawn");
						await RespawnVehicleAsync(dbId, data);
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[WorldVehicleMonitor] Error monitoring vehicle: {ex.Message}");
				}
			}
		}

		private async Task RespawnVehicleAsync(int dbId, WorldVehicleData data)
		{
			await Delay(RESPAWN_DELAY_MS);

			try
			{
				// Check if vehicle was respawned already
				if (data.EntityId != 0 && DoesEntityExist(data.EntityId))
				{
					Debug.WriteLine($"[WorldVehicleMonitor] Vehicle {dbId} already exists, skipping respawn");
					return;
				}

				Debug.WriteLine($"[WorldVehicleMonitor] Respawning vehicle DB ID {dbId}");
				_spawner.SpawnVehicle(data, dbId);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[WorldVehicleMonitor] Error respawning vehicle {dbId}: {ex.Message}");
			}
		}
	}
}
