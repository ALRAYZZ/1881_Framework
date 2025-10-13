using AdminManager.Client.Helpers;
using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace AdminManager.Client.Services
{
	internal sealed class VehicleLocator
	{
		private readonly EventHandlerDictionary _events;

		public VehicleLocator(EventHandlerDictionary events)
		{
			_events = events;
			_events["AdminManager:VehicleLocator:NearestVehicleResponse"] += new Action<int, float>(OnNearestVehicleResponse);
		}

		public void RequestNearestVehicle()
		{
			// Trigger CLIENT event to ask VehicleManager to find nearest vehicle
			BaseScript.TriggerEvent("VehicleManager:Client:GetNearestVehicle", "AdminManager:VehicleLocator:NearestVehicleResponse");
		}
		
		private void OnNearestVehicleResponse(int vehicleId, float distance)
		{
			if (vehicleId == 0)
			{
				ChatHelper.PrintError("No vehicles found nearby.");
				return;
			}

			// Get additional vehicle information
			if (DoesEntityExist(vehicleId))
			{
				uint modelHash = (uint)GetEntityModel(vehicleId);
				string modelName = GetDisplayNameFromVehicleModel(modelHash);
				string plate = GetVehicleNumberPlateText(vehicleId);
				var coords = GetEntityCoords(vehicleId, true);
				int netId = NetworkGetNetworkIdFromEntity(vehicleId);

				ChatHelper.PrintInfo($"=== Nearest Vehicle Info ===");
				ChatHelper.PrintInfo($"Entity ID: {vehicleId}");
				ChatHelper.PrintInfo($"Network ID: {netId}");
				ChatHelper.PrintInfo($"Model: {modelName} ({modelHash})");
				ChatHelper.PrintInfo($"Plate: {plate}");
				ChatHelper.PrintInfo($"Distance: {distance:F2}m");
				ChatHelper.PrintInfo($"Position: X={coords.X:F2}, Y={coords.Y:F2}, Z={coords.Z:F2}");
			}
			else
			{
				ChatHelper.PrintInfo($"Nearest vehicle ID: {vehicleId}, Distance: {distance:F2}m (entity no longer exists)");
			}
		}
	}
}
