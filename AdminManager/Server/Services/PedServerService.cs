using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using static CitizenFX.Core.Native.API;

namespace AdminManager.Server.Services
{
	internal sealed class PedServerService
	{
		public PedServerService()
		{
		}

		public void OnGetPedInfo([FromSource] Player player, int netId, float distance)
		{
			Debug.WriteLine($"[AdminManager] OnGetPedInfo called with Net ID: {netId}, distance: {distance:F2}m");
			int entity = NetworkGetEntityFromNetworkId(netId);
			if (entity == 0 || !DoesEntityExist(entity))
			{
				BaseScript.TriggerClientEvent(player, "AdminManager:Ped:InfoResponse", $"No ped found with Net ID: {netId}");
				return;
			}

			string modelName = GetEntityModel(entity).ToString("X");
			Vector3 pos = GetEntityCoords(entity);

			string info = $"=== Ped Info ===\n" +
						  $"Model: {modelName}" +
						  $"Net ID: {netId}\n" +
						  $"Distance (approx): {distance:F2}m\n" +
						  $"Position: X={pos.X:F2}, Y={pos.Y:F2}, Z={pos.Z:F2}";

			BaseScript.TriggerClientEvent(player, "AdminManager:Ped:InfoResponse", info);
		}
	}
}
