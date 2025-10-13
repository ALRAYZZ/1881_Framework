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
			// Ask VehicleManager to find nearest vehicle
			BaseScript.TriggerServerEvent("VehicleManager:Client:GetNearestVehicle", "AdminManager:NearestVehicleResponse");
		}
		
		public void OnNearestVehicleResponse(int vehicleId, float distance)
		{
			if (vehicleId == 0)
			{
				ChatHelper.PrintError("No vehicles found nearby.");
				return;
			}

			ChatHelper.PrintInfo($"Nearest vehicle ID: {vehicleId}, Distance: {distance} meters.");
		}
	}
}
