using System;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using VehicleManager.Server.Commands;
using VehicleManager.Server.Interfaces;
using VehicleManager.Server.Services;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Server
{
	/// <summary>
	/// Main entry point for VehicleManager server-side operations
	/// Orchestrates services and handles event registration
	/// </summary>
	public class ServerMain : BaseScript
	{
		// Core Services
		private readonly WorldVehicleRepository _repository;
		private readonly WorldVehicleTracker _tracker;
		private readonly WorldVehicleSpawner _spawner;
		private readonly WorldVehicleDiscovery _discovery;
		private readonly WorldVehicleMonitor _monitor;
		private readonly WorldVehicleStateManager _stateManager;

		// Commands
		private readonly VehicleCommands _vehicleCommands;

		// Spawn lock
		private bool _isSpawning = false;

		public ServerMain()
		{
			// Initialize database connection
			dynamic db = Exports["Database"];

			// Initialize services in dependency order
			_repository = new WorldVehicleRepository(db);
			_tracker = new WorldVehicleTracker();
			_spawner = new WorldVehicleSpawner(_tracker, _repository);
			_discovery = new WorldVehicleDiscovery(_repository, _tracker);
			_monitor = new WorldVehicleMonitor(_tracker, _spawner);
			_stateManager = new WorldVehicleStateManager(_tracker, _repository);

			// Initialize commands
			IVehicleManager vehicleManager = new Services.VehicleManager();
			_vehicleCommands = new VehicleCommands(vehicleManager, Players, db);

			// Register event handlers
			RegisterEventHandlers();

			Debug.WriteLine("[VehicleManager] Server initialized with service architecture");

			// Start vehicle system
			_ = InitializeWorldVehiclesAsync();
		}

		private void RegisterEventHandlers()
		{
			EventHandlers["VehicleManager:Server:SaveParkedVehicle"] +=
				new Action<Player, uint, string, string, float, float, float, float, float, float, float, int, int, int, string, string>(OnSaveParkedVehicle);

			EventHandlers["VehicleManager:Server:UnparkVehicle"] +=
				new Action<Player, int>(OnUnparkVehicle);

			EventHandlers["VehicleManager:Server:IsWorldVehicle"] +=
				new Action<Player, int, string>(OnIsWorldVehicle);

			EventHandlers["VehicleManager:Server:UpdateWorldVehicleEngineState"] +=
				new Action<Player, int, bool>(OnUpdateWorldVehicleEngineState);

			EventHandlers["VehicleManager:Server:SyncWorldVehiclesForPlayer"] +=
				new Action<string>(OnSyncWorldVehiclesForPlayer);

			EventHandlers["entityRemoved"] +=
				new Action<int>(OnEntityRemoved);

			EventHandlers["VehicleManager:Server:UpdateVehicleColors"] +=
				new Action<Player, int, int, int>(OnUpdateVehicleColors);

			Debug.WriteLine("[VehicleManager] Event handlers registered");
		}

		private async Task InitializeWorldVehiclesAsync()
		{
			try
			{
				// Allow existing vehicles to settle
				await Delay(2000);

				// Discover vehicles that already exist
				_discovery.DiscoverExistingVehicles();

				// Spawn vehicles from database
				await SpawnAllWorldVehiclesAsync();

				// Start health monitoring
				_monitor.StartMonitoring();

				Debug.WriteLine("[VehicleManager] World vehicle system fully initialized");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error initializing world vehicles: {ex.Message}");
			}
		}

		#region Event Handlers

		private void OnSaveParkedVehicle([FromSource] Player player, uint modelHash, string vehicleType, string plate,
			float x, float y, float z, float heading, float rx, float ry, float rz, int entityId,
			int primaryColor, int secondaryColor, string customPrimaryRGB, string customSecondaryRGB)
		{
			Debug.WriteLine($"[VehicleManager] OnSaveParkedVehicle - Model: {modelHash}, Plate: {plate}");

			var existingVehicle = _tracker.FindByModelAndPlate(modelHash, plate);

			if (existingVehicle != null)
			{
				int dbId = _tracker.GetAllVehicles()
					.FirstOrDefault(kvp => kvp.Value == existingVehicle).Key;

				Debug.WriteLine($"[VehicleManager] Updating existing world vehicle (DB ID: {dbId})");

				_stateManager.UpdateVehiclePosition(dbId, x, y, z, heading, rx, ry, rz,
					primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);

				player.TriggerEvent("chat:addMessage", new { args = new[] { $"Updated parked {vehicleType} position & colors (ID: {dbId})" } });
			}
			else
			{
				Debug.WriteLine($"[VehicleManager] Parking NEW world vehicle");

				_vehicleCommands.SaveVehicleToDatabase(player, modelHash, vehicleType, plate, x, y, z, heading, rx, ry, rz, entityId,
					primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);

				_ = Task.Run(async () =>
				{
					await Delay(500);
					await _spawner.SpawnVehicleForNewPark(modelHash, vehicleType, plate, x, y, z, heading,
						primaryColor, secondaryColor, customPrimaryRGB, customSecondaryRGB);
				});
			}
		}

		private void OnUnparkVehicle([FromSource] Player player, int vehicleNetId)
		{
			Debug.WriteLine($"[VehicleManager] OnUnparkVehicle - NetID {vehicleNetId}");

			if (_tracker.TryGetVehicleByNetId(vehicleNetId, out int dbId, out var vehicleData))
			{
				_vehicleCommands.RemoveVehicleFromDatabase(player, vehicleData.ModelHash, vehicleData.Plate);

				_tracker.UntrackVehicle(dbId);

				int vehicleEntity = NetworkGetEntityFromNetworkId(vehicleNetId);
				if (vehicleEntity != 0 && DoesEntityExist(vehicleEntity))
				{
					_tracker.MarkEntityAsIntentionallyDeleted(vehicleEntity);
					DeleteEntity(vehicleEntity);
				}

				player.TriggerEvent("chat:addMessage", new { args = new[] { $"Unparked vehicle (DB ID: {dbId}, Plate: {vehicleData.Plate})" } });
				Debug.WriteLine($"[VehicleManager] Unparked vehicle (DB ID: {dbId}, NetID: {vehicleNetId})");
			}
			else
			{
				player.TriggerEvent("chat:addMessage", new { args = new[] { "Error: This is not a parked world vehicle." } });
				Debug.WriteLine($"[VehicleManager] ERROR: Vehicle NetID {vehicleNetId} is not tracked");
			}
		}

		private void OnIsWorldVehicle([FromSource] Player player, int vehicleNetId, string callbackEvent)
		{
			try
			{
				Debug.WriteLine($"[VehicleManager] IsWorldVehicle query for NetID {vehicleNetId}");
				_tracker.LogTrackingState();

				if (_tracker.TryGetDbIdFromNetId(vehicleNetId, out int dbId))
				{
					Debug.WriteLine($"[VehicleManager] ✅ NetID {vehicleNetId} IS a world vehicle (DB ID: {dbId})");
					player.TriggerEvent(callbackEvent, true, dbId, vehicleNetId);
				}
				else
				{
					Debug.WriteLine($"[VehicleManager] ❌ NetID {vehicleNetId} is NOT a world vehicle");
					player.TriggerEvent(callbackEvent, false, 0, vehicleNetId);
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in OnIsWorldVehicle: {ex.Message}");
				player.TriggerEvent(callbackEvent, false, 0, vehicleNetId);
			}
		}

		private void OnUpdateWorldVehicleEngineState([FromSource] Player player, int vehicleNetId, bool engineOn)
		{
			try
			{
				_stateManager.UpdateEngineState(vehicleNetId, engineOn);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in OnUpdateWorldVehicleEngineState: {ex.Message}");
			}
		}

		private void OnSyncWorldVehiclesForPlayer(string playerHandle)
		{
			var player = Players[playerHandle];
			if (player == null) return;
			_stateManager.SyncWorldVehiclesToPlayer(player);
		}

		private void OnEntityRemoved(int entity)
		{
			_monitor.HandleEntityRemoval(entity);
		}

		private void OnUpdateVehicleColors([FromSource] Player player, int vehicleNetId, int primaryColor, int secondaryColor)
		{
			try
			{
				Debug.WriteLine($"[VehicleManager] Color change request - NetID: {vehicleNetId}, Primary: {primaryColor}, Secondary: {secondaryColor}");

				if (_tracker.TryGetVehicleByNetId(vehicleNetId, out int dbId, out var vehicleData))
				{
					// Update in-memory tracking
					_tracker.UpdateVehiclePosition(dbId, vehicleData.X, vehicleData.Y, vehicleData.Z, vehicleData.Heading,
						 primaryColor, secondaryColor, null, null);

					// Persist to database
					_vehicleCommands.UpdateVehicleColors(dbId, primaryColor, secondaryColor);

					// Broadcast to all clients to update colors
					TriggerClientEvent("VehicleManager:Client:ApplyVehicleColors", vehicleNetId, primaryColor, secondaryColor);

					player.TriggerEvent("chat:addMessage", new { args = new[] { $"Updated vehicle colors (DB ID: {dbId})" } });
					Debug.WriteLine($"[VehicleManager] Updated vehicle colors (DB ID: {dbId})");
				}
				else
				{
					Debug.WriteLine($"[VehicleManager] ERROR: Vehicle NetID {vehicleNetId} is not a World Vehicle");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in OnUpdateVehicleColors: {ex.Message}");
			}
		}

		#endregion

		#region Spawn Logic

		private async Task SpawnAllWorldVehiclesAsync()
		{
			if (_isSpawning)
			{
				Debug.WriteLine("[VehicleManager] Already spawning vehicles, skipping");
				return;
			}

			_isSpawning = true;

			try
			{
				await Delay(1000);

				_repository.GetAllWorldVehicles((vehicles) =>
				{
					int spawned = 0;
					int skipped = 0;

					foreach (var vehicleData in vehicles)
					{
						try
						{
							int dbId = vehicleData.EntityId; // Repository returns DB ID in EntityId temporarily

							if (_tracker.TryGetVehicleByDbId(dbId, out _))
							{
								skipped++;
								continue;
							}

							int veh = _spawner.SpawnVehicle(vehicleData, dbId);
							if (veh != 0)
							{
								spawned++;
							}
						}
						catch (Exception ex)
						{
							Debug.WriteLine($"[VehicleManager] Error spawning vehicle: {ex.Message}");
						}
					}

					if (skipped > 0)
					{
						Debug.WriteLine($"[VehicleManager] Skipped {skipped} vehicles already in world");
					}
					Debug.WriteLine($"[VehicleManager] Spawned {spawned} new world vehicles");
				});
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in SpawnAllWorldVehiclesAsync: {ex.Message}");
			}
			finally
			{
				_isSpawning = false;
			}
		}

		#endregion
	}
}