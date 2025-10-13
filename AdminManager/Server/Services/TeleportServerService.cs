using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using static CitizenFX.Core.Native.API;
using AdminManager.Server.StaticHelpers;

namespace AdminManager.Server.Services
{
	internal sealed class TeleportServerService
	{
		private readonly BaseScript _script;

		public TeleportServerService(BaseScript script)
		{
			_script = script;
		}

		public void OnTeleportByEntityId(Player player, int entityId, float x, float y, float z)
		{
			try
			{
				bool success = TeleportEntity(entityId, x, y, z);
				AdminResultSender.SendResult(player, success, success ? $"Entity with Entity ID: {entityId}, teleported successfully." : "Failed to teleport entity.");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[AdminManager] Error in OnTeleportByEntityId: {ex.Message}");
				AdminResultSender.SendResult(player, false, "Server error while teleporting by entityID");
			}
		}

		public void OnTeleportByNetId(Player player, int netId, float x, float y, float z)
		{
			try
			{
				int entity = NetworkGetEntityFromNetworkId(netId);
				bool success = entity != 0 && TeleportEntity(entity, x, y, z);
				AdminResultSender.SendResult(player, success, success ? $"Entity with Net ID: {netId}, teleported successfully." : "Failed to teleport entity.");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[AdminManager] Error in OnTeleportByNetId: {ex.Message}");
				AdminResultSender.SendResult(player, false, "Server error while teleporting by netID");
			}
		}

		private static bool TeleportEntity(int entity, float x, float y, float z)
		{
			if (entity == 0 || !DoesEntityExist(entity))
			{
				return false;
			}

			try
			{
				FreezeEntityPosition(entity, true);
			}
			catch { /* Ignore errors */ }

			SetEntityCoords(entity, x, y, z, false, false, false, true);

			try
			{
				FreezeEntityPosition(entity, false);
			}
			catch { /* Ignore errors */ }

			return true;
		}
	}

}
