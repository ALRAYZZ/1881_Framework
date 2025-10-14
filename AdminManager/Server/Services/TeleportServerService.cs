using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Text;
using AdminManager.Server.StaticHelpers;
using static CitizenFX.Core.Native.API;

namespace AdminManager.Server.Services
{
	internal sealed class TeleportServerService
	{
		public TeleportServerService()
		{
		}

		public void OnGoToEntity([FromSource]Player player, int netId)
		{
			int entity = NetworkGetEntityFromNetworkId(netId);
			if (entity == 0 || !DoesEntityExist(entity))
			{
				AdminResultSender.SendResult(player, false, $"No entity found with Net ID: {netId}");
				return;
			}

			Vector3 targetPos = GetEntityCoords(entity);
			int ped = GetPlayerPed(player.Handle);

			if (ped == 0)
			{
				AdminResultSender.SendResult(player, false, "Player ped not found.");
				return;
			}

			SetEntityCoords(ped, targetPos.X, targetPos.Y, targetPos.Z, false, false, false, true);
			AdminResultSender.SendResult(player, true, $"Teleported to entity with Net ID: {netId} at position: {targetPos.X}, {targetPos.Y}, {targetPos.Z}");
		}

		public void OnBringEntity([FromSource]Player player, int netId)
		{
			int entity = NetworkGetEntityFromNetworkId(netId);
			if (entity == 0 || !DoesEntityExist(entity))
			{
				AdminResultSender.SendResult(player, false, $"No entity found with Net ID: {netId}");
				return;
			}

			int ped = GetPlayerPed(player.Handle);
			if (ped == 0)
			{
				AdminResultSender.SendResult(player, false, "Player ped not found.");
				return;
			}

			Vector3 pos = GetEntityCoords(ped);

			SetEntityCoords(entity, pos.X, pos.Y, pos.Z, false, false, false, true);
			AdminResultSender.SendResult(player, true, $"Brought entity with Net ID: {netId} to your position: {pos.X}, {pos.Y}, {pos.Z}");
		}

		public void OnTeleportByNetId([FromSource]Player player, int netId, float x, float y, float z)
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
