using System;
using System.Collections.Generic;
using System.Text;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PedManager.Server.Services
{
	internal sealed class PedAnimationServer
	{
		public PedAnimationServer()
		{
		}

		public void GiveAnimation(int netId, string animDict, string animName, int animFlag)
		{
			int entity = NetworkGetEntityFromNetworkId(netId);
			if (entity == 0 || !DoesEntityExist(entity))
			{
				Debug.WriteLine($"[PedManager] GiveAnimation: entity for netId {netId} not found.");
				return;
			}

			// Broadcast to all clients
			BaseScript.TriggerClientEvent("PedManager:Client:PlayAnimation", netId, animDict, animName, animFlag);

		}
	}
}
