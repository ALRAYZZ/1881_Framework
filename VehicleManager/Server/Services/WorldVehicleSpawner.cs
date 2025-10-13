using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using VehicleManager.Server.Models;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Server.Services
{
	/// <summary>
	/// Handles spawning and color application for world vehicles
	/// </summary>
	public class WorldVehicleSpawner
	{
		private readonly WorldVehicleTracker _tracker;
		private readonly WorldVehicleRepository _repository;

		public WorldVehicleSpawner(WorldVehicleTracker tracker, WorldVehicleRepository repository)
		{
			_tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
			_repository = repository ?? throw new ArgumentNullException(nameof(repository));
		}

		public int SpawnVehicle(WorldVehicleData data, int dbId)
		{
			float zSpawn = data.Z + 1.0f;

			int veh = Function.Call<int>(Hash.CREATE_VEHICLE_SERVER_SETTER,
				data.ModelHash, data.VehicleType, data.X, data.Y, zSpawn, data.Heading);

			if (veh == 0)
			{
				Debug.WriteLine($"[WorldVehicleSpawner] Failed to spawn vehicle for DB ID {dbId}");
				return 0;
			}

			// Set plate
			if (!string.IsNullOrEmpty(data.Plate))
			{
				SetVehicleNumberPlateText(veh, data.Plate);
			}

			// Apply colors
			ApplyVehicleColors(veh, data.PrimaryColor, data.SecondaryColor,
				data.CustomPrimaryRGB, data.CustomSecondaryRGB);

			// Get network ID
			int netId = NetworkGetNetworkIdFromEntity(veh);

			if (netId != 0)
			{
				// Update tracking
				data.NetId = netId;
				data.EntityId = veh;
				_tracker.TrackVehicle(dbId, data);

				// Ensure vehicle_data exists in DB
				_repository.EnsureVehicleDataExists(dbId, data.EngineOn);

				// Broadcast to clients
				BaseScript.TriggerClientEvent("VehicleManager:Client:RegisterWorldVehicle", netId, dbId);
				BaseScript.TriggerClientEvent("VehicleManager:Client:SetWorldVehicleEngineState", netId, data.EngineOn);

				Debug.WriteLine($"[WorldVehicleSpawner] Spawned vehicle - DB ID: {dbId}, Entity: {veh}, NetID: {netId}, EngineOn: {data.EngineOn}");
			}
			else
			{
				Debug.WriteLine($"[WorldVehicleSpawner] Failed to get network ID for spawned vehicle (DB ID: {dbId})");
			}

			return veh;
		}

		public async Task<int> SpawnVehicleForNewPark(uint modelHash, string vehicleType, string plate,
			float x, float y, float z, float heading, int primaryColor, int secondaryColor,
			string customPrimaryRGB, string customSecondaryRGB)
		{
			return await Task.Run(() =>
			{
				int vehicleEntity = 0;

				_repository.GetVehicleByModelAndPlate(modelHash, plate, (vehicleData) =>
				{
					if (vehicleData == null)
					{
						Debug.WriteLine($"[WorldVehicleSpawner] Vehicle not found in database after park");
						return;
					}

					int dbId = vehicleData.EntityId; // Repository returns DB ID in EntityId field temporarily

					if (_tracker.TryGetVehicleByDbId(dbId, out _))
					{
						Debug.WriteLine($"[WorldVehicleSpawner] Vehicle {dbId} already spawned, skipping");
						return;
					}

					// Build complete vehicle data
					var fullVehicleData = new WorldVehicleData
					{
						ModelHash = modelHash,
						VehicleType = vehicleType,
						Plate = plate,
						X = x,
						Y = y,
						Z = z,
						Heading = heading,
						PrimaryColor = primaryColor,
						SecondaryColor = secondaryColor,
						CustomPrimaryRGB = customPrimaryRGB,
						CustomSecondaryRGB = customSecondaryRGB,
						EngineOn = vehicleData.EngineOn
					};

					vehicleEntity = SpawnVehicle(fullVehicleData, dbId);
				});

				return vehicleEntity;
			});
		}

		private void ApplyVehicleColors(int veh, int primaryColor, int secondaryColor,
			string customPrimaryRGB, string customSecondaryRGB)
		{
			SetVehicleColours(veh, primaryColor, secondaryColor);

			if (!string.IsNullOrEmpty(customPrimaryRGB))
			{
				var rgb = customPrimaryRGB.Split(',');
				if (rgb.Length == 3)
				{
					int r = int.Parse(rgb[0]);
					int g = int.Parse(rgb[1]);
					int b = int.Parse(rgb[2]);
					SetVehicleCustomPrimaryColour(veh, r, g, b);
				}
			}

			if (!string.IsNullOrEmpty(customSecondaryRGB))
			{
				var rgb = customSecondaryRGB.Split(',');
				if (rgb.Length == 3)
				{
					int r = int.Parse(rgb[0]);
					int g = int.Parse(rgb[1]);
					int b = int.Parse(rgb[2]);
					SetVehicleCustomSecondaryColour(veh, r, g, b);
				}
			}

			Debug.WriteLine($"[WorldVehicleSpawner] Applied colors to vehicle {veh} (Primary: {primaryColor}, Secondary: {secondaryColor})");
		}
	}
}