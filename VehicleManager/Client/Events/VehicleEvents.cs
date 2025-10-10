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
	public class VehicleEvents
	{
		private readonly VehicleFactory _vehicleFactory;
		private readonly EventHandlerDictionary _eventHandlers;

		public VehicleEvents(VehicleFactory vehicleFactory, EventHandlerDictionary eventHandlers)
		{
			_vehicleFactory = vehicleFactory;
			_eventHandlers = eventHandlers;

			Debug.WriteLine("[VehicleManager] Registering client event handlers...");

			// Register server-triggered events
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

			_eventHandlers["VehicleManager:Client:SetVehicleOnGround"] += new Action<int>((netId) =>
			{
				SetVehicleOnGroundProperly(netId);
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

			// Determine vehicle type based on vehicle class
			string vehicleType = DetermineVehicleType(veh);

			// Send data to server including vehicle type and entity ID
			BaseScript.TriggerServerEvent("VehicleManager:Server:SaveParkedVehicle",
				modelHash, vehicleType, plate, pos.X, pos.Y, pos.Z, heading, rot.X, rot.Y, rot.Z, veh);
		}

		private void UnparkCurrentVehicle()
		{
			// POTENTIAL REFACTOR INTO FUNCTION ALL THIS DRIVER CHECKS
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

			uint modelHash = (uint)GetEntityModel(veh);
			string plate = GetVehicleNumberPlateText(veh);

			// Send data to server to remove from database (vehicle stays in game)
			BaseScript.TriggerServerEvent("VehicleManager:Server:UnparkVehicle", modelHash, plate);
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

		private void SetVehicleOnGroundProperly(int netId)
		{
			int veh = NetworkGetEntityFromNetworkId(netId);
			if (veh != 0 && DoesEntityExist(veh))
			{
				SetVehicleOnGroundProperly(veh);
			}
		}
	}
}
