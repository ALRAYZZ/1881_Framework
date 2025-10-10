using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VehicleManager.Client.Services;
using static CitizenFX.Core.Native.API;

namespace VehicleManager.Client.Events
{
	public class VehicleEvents : BaseScript
	{
		private readonly VehicleFactory _vehicleFactory;
		private readonly EventHandlerDictionary _eventHandlers;

		// Pending unpark request
		private int _pendingUnparkVehicle = 0;

		public VehicleEvents(VehicleFactory vehicleFactory, EventHandlerDictionary eventHandlers)
		{
			_vehicleFactory = vehicleFactory;
			_eventHandlers = eventHandlers;

			Debug.WriteLine("[VehicleManager] Registering client event handlers...");

			_eventHandlers["VehicleManager:Client:SpawnVehicle"] += new Action<string>(async (modelName) =>
			{
				Debug.WriteLine($"[VehicleManager] Client received SpawnModel event for model: {modelName}");
				await _vehicleFactory.SpawnVehicleAsync(modelName);
			});

			_eventHandlers["VehicleManager:Client:DeleteNearestVehicle"] += new Action(() =>
			{
				Debug.WriteLine("[VehicleManager] Client received DeleteNearestVehicle event.");
				DeleteNearestVehicle();
			});

			_eventHandlers["VehicleManager:Client:RequestParkVehicle"] += new Action(() =>
			{
				Debug.WriteLine("[VehicleManager] Client received RequestParkVehicle event.");
				ParkCurrentVehicle();
			});

			_eventHandlers["VehicleManager:Client:RequestUnparkVehicle"] += new Action(() =>
			{
				Debug.WriteLine("[VehicleManager] Client received RequestUnparkVehicle event.");
				UnparkCurrentVehicle();
			});

			// NEW: Callback from server about world vehicle status
			_eventHandlers["VehicleManager:Client:WorldVehicleQueryResult"] += new Action<bool, int, int>((isWorldVehicle, dbId, vehicleEntity) =>
			{
				OnWorldVehicleQueryResult(isWorldVehicle, dbId, vehicleEntity);
			});

			Debug.WriteLine("[VehicleManager] Client event handlers registered.");
		}

		private void DeleteNearestVehicle()
		{
			var ped = Game.PlayerPed;
			var pos = ped.Position;

			int vehicleHandle = GetClosestVehicle(pos.X, pos.Y, pos.Z, 5.0f, 0, 70);

			if (vehicleHandle != 0)
			{
				_vehicleFactory.DeleteVehicle(vehicleHandle);
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Deleted nearest vehicle." } });
			}
			else
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "No vehicle found nearby." } });
			}
		}

		private void ParkCurrentVehicle()
		{
			int ped = PlayerPedId();
			if (ped == 0)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: Could not find your ped." } });
				return;
			}

			int veh = GetVehiclePedIsIn(ped, false);
			if (veh == 0)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: You are not in a vehicle." } });
				return;
			}

			if (GetPedInVehicleSeat(veh, -1) != ped)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: You must be the driver to park the vehicle." } });
				return;
			}

			uint modelHash = (uint)GetEntityModel(veh);
			string plate = GetVehicleNumberPlateText(veh);

			var pos = GetEntityCoords(veh, true);
			float heading = GetEntityHeading(veh);
			var rot = GetEntityRotation(veh, 0);

			// Determine vehicle type
			string vehicleType = DetermineVehicleType(veh);

			// Get vehicle colors
			int primaryColor = 0, secondaryColor = 0;
			GetVehicleColours(veh, ref primaryColor, ref secondaryColor);

			// Get custom RGB colors (if any)
			int customPrimaryR = 0, customPrimaryG = 0, customPrimaryB = 0;
			int customSecondaryR = 0, customSecondaryG = 0, customSecondaryB = 0;
			GetVehicleCustomPrimaryColour(veh, ref customPrimaryR, ref customPrimaryG, ref customPrimaryB);
			GetVehicleCustomSecondaryColour(veh, ref customSecondaryR, ref customSecondaryG, ref customSecondaryB);

			// Format RGB as "r,g,b" or null if not custom
			string customPrimaryRGB = null;
			string customSecondaryRGB = null;

			// Check if vehicle has custom colors (RGB values != 0,0,0)
			if (customPrimaryR != 0 || customPrimaryG != 0 || customPrimaryB != 0)
			{
				customPrimaryRGB = $"{customPrimaryR},{customPrimaryG},{customPrimaryB}";
			}

			if (customSecondaryR != 0 || customSecondaryG != 0 || customSecondaryB != 0)
			{
				customSecondaryRGB = $"{customSecondaryR},{customSecondaryG},{customSecondaryB}";
			}

			// Exit vehicle and delete it BEFORE sending to server
			Debug.WriteLine($"[VehicleManager] Exiting and deleting client vehicle {veh} before parking");

			// Exit vehicle instantly
			TaskLeaveVehicle(ped, veh, 16); // 16 = instant exit

			// Teleport player in front of where the vehicle was
			float offsetX = pos.X + (float)(Math.Sin(heading * Math.PI / 180.0) * 3.0);
			float offsetY = pos.Y + (float)(Math.Cos(heading * Math.PI / 180.0) * 3.0);
			SetEntityCoords(ped, offsetX, offsetY, pos.Z, false, false, false, true);
			SetEntityHeading(ped, heading + 180.0f); // Face where the vehicle was

			Debug.WriteLine($"[VehicleManager] Player teleported to ({offsetX}, {offsetY}, {pos.Z})");

			// Delete the client vehicle immediately
			_vehicleFactory.DeleteVehicle(veh);
			
			Debug.WriteLine($"[VehicleManager] Client vehicle {veh} deleted");

			// Send data to server including colors
			BaseScript.TriggerServerEvent("VehicleManager:Server:SaveParkedVehicle",
				modelHash, vehicleType, plate, pos.X, pos.Y, pos.Z, heading, rot.X, rot.Y, rot.Z, 0, // entityId = 0 since we deleted it
				primaryColor, secondaryColor, customPrimaryRGB ?? "", customSecondaryRGB ?? "");

			BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Vehicle parked! Server vehicle will spawn momentarily." } });
		}

		private void UnparkCurrentVehicle()
		{
			int ped = PlayerPedId();
			if (ped == 0)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: Could not find your ped." } });
				return;
			}

			int veh = GetVehiclePedIsIn(ped, false);
			if (veh == 0)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: You are not in a vehicle." } });
				return;
			}

			if (GetPedInVehicleSeat(veh, -1) != ped)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: You must be the driver to unpark the vehicle." } });
				return;
			}

			// Get NETWORK ID instead of entity ID
			int netId = NetworkGetNetworkIdFromEntity(veh);
			
			if (netId == 0)
			{
				BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: Could not get network ID for vehicle." } });
				Debug.WriteLine($"[VehicleManager] ERROR: Failed to get network ID for entity {veh}");
				return;
			}

			// Store pending unpark request
			_pendingUnparkVehicle = netId;

			// Get vehicle details for debugging
			uint modelHash = (uint)GetEntityModel(veh);
			string plate = GetVehicleNumberPlateText(veh);

			Debug.WriteLine($"[VehicleManager] ====== Unpark Request ======");
			Debug.WriteLine($"[VehicleManager] Vehicle Entity ID: {veh}");
			Debug.WriteLine($"[VehicleManager] Vehicle NetID: {netId}");
			Debug.WriteLine($"[VehicleManager] Vehicle Model: {modelHash}");
			Debug.WriteLine($"[VehicleManager] Vehicle Plate: {plate}");
			Debug.WriteLine($"[VehicleManager] Querying server if NetID {netId} is a world vehicle...");
			Debug.WriteLine($"[VehicleManager] ============================");

			// Query the server to check if this is a world vehicle using NETWORK ID
			BaseScript.TriggerServerEvent("VehicleManager:Server:IsWorldVehicle", netId, "VehicleManager:Client:WorldVehicleQueryResult");
		}

		private void OnWorldVehicleQueryResult(bool isWorldVehicle, int dbId, int vehicleNetId)
		{
			try
			{
				Debug.WriteLine($"[VehicleManager] ====== Query Result ======");
				Debug.WriteLine($"[VehicleManager] Received query result:");
				Debug.WriteLine($"[VehicleManager]   isWorldVehicle: {isWorldVehicle}");
				Debug.WriteLine($"[VehicleManager]   dbId: {dbId}");
				Debug.WriteLine($"[VehicleManager]   vehicleNetId: {vehicleNetId}");
				Debug.WriteLine($"[VehicleManager]   _pendingUnparkVehicle: {_pendingUnparkVehicle}");

				// Check if this is the vehicle we're trying to unpark
				if (_pendingUnparkVehicle != vehicleNetId)
				{
					Debug.WriteLine($"[VehicleManager] ❌ Query result for different vehicle (expected {_pendingUnparkVehicle}, got {vehicleNetId})");
					return;
				}

				// Clear pending request
				_pendingUnparkVehicle = 0;

				if (isWorldVehicle)
				{
					Debug.WriteLine($"[VehicleManager] ✅ Vehicle NetID {vehicleNetId} is a world vehicle (DB ID: {dbId}), proceeding with unpark");
					
					// Send unpark request to server using NETWORK ID
					BaseScript.TriggerServerEvent("VehicleManager:Server:UnparkVehicle", vehicleNetId);
					
					BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Vehicle unparked!" } });
				}
				else
				{
					Debug.WriteLine($"[VehicleManager] ❌ Vehicle NetID {vehicleNetId} is NOT a world vehicle");
					BaseScript.TriggerEvent("chat:addMessage", new { args = new[] { "Error: This is not a parked world vehicle." } });
				}
				
				Debug.WriteLine($"[VehicleManager] ==========================");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VehicleManager] Error in OnWorldVehicleQueryResult: {ex.Message}");
			}
		}

		private string DetermineVehicleType(int vehicle)
		{
			int vehicleClass = GetVehicleClass(vehicle);

			switch (vehicleClass)
			{
				case 8:  // Motorcycles
					return "bike";
				case 14: // Boats
					return "boat";
				case 15: // Helicopters
					return "heli";
				case 16: // Planes
					return "plane";
				case 11: // Utility/Trailers
					if (IsThisModelABicycle((uint)GetEntityModel(vehicle)))
						return "bike";
					return "automobile";
				default:
					return "automobile";
			}
		}
	}
}
