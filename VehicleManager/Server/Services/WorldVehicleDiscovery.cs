using System;
using System.Collections.Generic;
using CitizenFX.Core;
using VehicleManager.Server.Models;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Server.Services
{
	/// <summary>
	/// Discovers existing vehicles in the world on server startup
	/// </summary>
	public class WorldVehicleDiscovery
	{
		private readonly WorldVehicleRepository _repository;
		private readonly WorldVehicleTracker _tracker;

		public WorldVehicleDiscovery(WorldVehicleRepository repository, WorldVehicleTracker tracker)
		{
			_repository = repository ?? throw new ArgumentNullException(nameof(repository));
			_tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
		}

		public void DiscoverExistingVehicles()
		{
			Debug.WriteLine("[WorldVehicleDiscovery] Discovering existing vehicles in the world...");

			var existingEntities = GetAllVehicleEntities();
			Debug.WriteLine($"[WorldVehicleDiscovery] Found {existingEntities.Count} vehicle entities");

			_repository.GetAllWorldVehicles((dbVehicles) =>
			{
				int discovered = 0;

				foreach (var dbVehicle in dbVehicles)
				{
					int dbId = dbVehicle.EntityId; // EntityId contains DB ID from repository

					foreach (int entity in existingEntities)
					{
						if (!DoesEntityExist(entity)) continue;

						if (IsVehicleMatch(entity, dbVehicle))
						{
							int netId = NetworkGetNetworkIdFromEntity(entity);

							dbVehicle.NetId = netId;
							dbVehicle.EntityId = entity;

							_tracker.TrackVehicle(dbId, dbVehicle);
							_repository.EnsureVehicleDataExists(dbId, dbVehicle.EngineOn);

							// Broadcast to clients
							BaseScript.TriggerClientEvent("VehicleManager:Client:RegisterWorldVehicle", netId, dbId);
							BaseScript.TriggerClientEvent("VehicleManager:Client:SetWorldVehicleEngineState", netId, dbVehicle.EngineOn);

							discovered++;
							Debug.WriteLine($"[WorldVehicleDiscovery] Discovered vehicle - DB ID: {dbId}, Entity: {entity}, NetID: {netId}, Plate: {dbVehicle.Plate}");
							break;
						}
					}
				}

				Debug.WriteLine($"[WorldVehicleDiscovery] Discovered {discovered} existing world vehicles");
			});
		}

		private List<int> GetAllVehicleEntities()
		{
			var entities = new List<int>();
			var allVehiclesObj = GetAllVehicles();

			foreach (var vehObj in allVehiclesObj)
			{
				try
				{
					int veh = Convert.ToInt32(vehObj);
					if (DoesEntityExist(veh))
					{
						entities.Add(veh);
					}
				}
				catch { }
			}

			return entities;
		}

		private bool IsVehicleMatch(int entity, WorldVehicleData dbVehicle)
		{
			uint entityModel = (uint)GetEntityModel(entity);
			string entityPlate = GetVehicleNumberPlateText(entity);

			bool modelMatch = entityModel == dbVehicle.ModelHash;
			bool plateMatch = string.Equals(entityPlate?.Trim(), dbVehicle.Plate?.Trim(),
				StringComparison.OrdinalIgnoreCase);

			return modelMatch && plateMatch;
		}
	}
}