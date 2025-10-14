using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace AdminManager.Server.Services
{
	internal sealed class VehicleServerService
	{
		public VehicleServerService()
		{
		}

		public void OnGetVehicleInfo([FromSource]Player player, int netId, float distance)
		{
			Debug.WriteLine($"[AdminManager] OnGetVehicleInfo called with Net ID: {netId}, distance: {distance:F2}m");
			int entity = NetworkGetEntityFromNetworkId(netId);
			if (entity == 0 || !DoesEntityExist(entity))
			{
				BaseScript.TriggerClientEvent(player, "AdminManager:Vehicle:InfoResponse", $"No vehicle found with Net ID: {netId}");
				return;
			}

			uint modelHash = (uint)GetEntityModel(entity);
			string plate = GetVehicleNumberPlateText(entity);
			Vector3 pos = GetEntityCoords(entity);
			int owner = NetworkGetEntityOwner(entity);

			string info = $"=== Vehicle Info ===\n" +
						  $"Net ID: {netId}\n" +
						  $"Plate: {plate}\n" +
						  $"Owner: {owner}\n" +
						  $"Distance (approx): {distance:F2}m\n" +
						  $"Position: X={pos.X:F2}, Y={pos.Y:F2}, Z={pos.Z:F2}";

			BaseScript.TriggerClientEvent(player, "AdminManager:Vehicle:InfoResponse", info);
		}
	}
}
