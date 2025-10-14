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

			// Existing nearest vehicle response handler
			_events["AdminManager:VehicleLocator:NearestVehicleResponse"] += new Action<int, float>(OnNearestVehicleResponse);

			// Listen for server response
			_events["AdminManager:Vehicle:InfoResponse"] += new Action<string>(OnVehicleInfoResponse);

		}

		private void OnVehicleInfoResponse(string info)
		{
			ChatHelper.PrintInfo(info);
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

			int netId = NetworkGetNetworkIdFromEntity(vehicleId);
			if (netId == 0)
			{
				ChatHelper.PrintError("Failed to get network ID for the nearest vehicle.");
				return;
			}

			// Send the Net ID to the server for full info
			BaseScript.TriggerServerEvent("AdminManager:Vehicle:GetInfo", netId, distance);
			Debug.WriteLine($"[AdminManager] Requested info for vehicle Net ID: {netId}, distance: {distance:F2}m" );
		}
	}
}
