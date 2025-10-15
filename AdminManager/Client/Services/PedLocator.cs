using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdminManager.Client.Helpers;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace AdminManager.Client.Services
{
	internal sealed class PedLocator
	{
		private readonly EventHandlerDictionary _events;

		public PedLocator(EventHandlerDictionary events)
		{
			_events = events;

			// Existing nearest ped response handler
			_events["AdminManager:PedLocator:NearestPedResponse"] += new Action<int, float>(OnNearestPedResponse);
			// Listen for server response
			_events["AdminManager:Ped:InfoResponse"] += new Action<string>(OnPedInfoResponse);
		}

		public void RequestNearestPed()
		{
			// Trigger CLIENT event to ask VehicleManager to find nearest vehicle
			BaseScript.TriggerEvent("PedManager:Client:GetNearestPed", "AdminManager:PedLocator:NearestPedResponse");
		}

		private void OnPedInfoResponse(string info)
		{
			ChatHelper.PrintInfo(info);
		}

		private void OnNearestPedResponse(int pedId, float distance)
		{
			if (pedId == 0)
			{
				ChatHelper.PrintError("No peds found nearby.");
				return;
			}

			int netId = NetworkGetNetworkIdFromEntity(pedId);
			if (netId == 0)
			{
				ChatHelper.PrintError("Failed to get network ID for the nearest ped.");
				return;
			}

			// Send the Net ID to the server for full info
			BaseScript.TriggerServerEvent("AdminManager:Ped:GetInfo", netId, distance);
			Debug.WriteLine($"[AdminManager] Requested info for ped Net ID: {netId}, distance: {distance:F2}m");
		}
	}
}
