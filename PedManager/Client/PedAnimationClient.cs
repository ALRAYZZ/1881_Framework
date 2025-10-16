using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PedManager.Client
{
	internal sealed class PedAnimationClient
	{
		public PedAnimationClient()
		{ 
		}

		public async Task PlayAnimation(int netId, string animDict, string animName, int animFlag)
		{
			int ped = NetworkGetEntityFromNetworkId(netId);
			if (!DoesEntityExist(ped) || !IsEntityAPed(ped))
			{
				return;
			}
			RequestAnimDict(animDict);

			int startTime = GetGameTimer();
			const int timeoutMs = 5000; // 5 seconds timeout
			while (!HasAnimDictLoaded(animDict) && (GetGameTimer() - startTime) < timeoutMs)
			{
				await BaseScript.Delay(10);
			}

			if (!HasAnimDictLoaded(animDict))
			{
				Debug.WriteLine($"[PedManager] PlayAnimation: Failed to load animDict '{animDict}' within timeout.");
				return;
			}

			TaskPlayAnim(ped, animDict, animName, 8.0f, -8.0f, -1, animFlag, 0, false, false, false);
			Debug.WriteLine($"[PedManager] PlayAnimation: Playing animation '{animName}' from dict '{animDict}' on ped (netId: {netId}).");
		}

	}
}
