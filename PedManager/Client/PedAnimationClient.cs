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
			if (ped == 0)
			{
				Debug.WriteLine($"[PedManager] PlayAnimation: NetworkGetEntityFromNetworkId returned 0 for netId {netId}. Ped not streamed on this client.");
				return;
			}
			if (!DoesEntityExist(ped))
			{
				Debug.WriteLine($"[PedManager] PlayAnimation: Entity does not exist for netId {netId}.");
				return;
			}
			if (!IsEntityAPed(ped))
			{
				Debug.WriteLine($"[PedManager] PlayAnimation: Entity for netId {netId} is not a ped.");
				return;
			}

			// Check if already loaded
			if (HasAnimDictLoaded(animDict))
			{
				Debug.WriteLine($"[PedManager] PlayAnimation: animDict '{animDict}' already loaded.");
			}
			else
			{
				Debug.WriteLine($"[PedManager] PlayAnimation: Requesting animDict '{animDict}' for ped netId {netId}.");
				RequestAnimDict(animDict);

				int startTime = GetGameTimer();
				const int timeoutMs = 10000; // Increased to 10 seconds
				while (!HasAnimDictLoaded(animDict) && (GetGameTimer() - startTime) < timeoutMs)
				{
					await BaseScript.Delay(10);
				}

				if (!HasAnimDictLoaded(animDict))
				{
					Debug.WriteLine($"[PedManager] PlayAnimation: Failed to load animDict '{animDict}' within {timeoutMs}ms timeout.");
					return;
				}
				Debug.WriteLine($"[PedManager] PlayAnimation: Successfully loaded animDict '{animDict}' in {(GetGameTimer() - startTime)}ms.");
			}

			TaskPlayAnim(ped, animDict, animName, 8.0f, -8.0f, -1, animFlag, 0, false, false, false);
			Debug.WriteLine($"[PedManager] PlayAnimation: Playing animation '{animName}' from dict '{animDict}' on ped (netId: {netId}).");

			//Remove the dict after a delay to free memory (uncomment if needed)
			// _ = RemoveAnimDictDelayed(animDict);
		}

		// private async Task RemoveAnimDictDelayed(string animDict)
		// {
		//     await BaseScript.Delay(5000); // Wait 5 seconds
		//     RemoveAnimDict(animDict);
		//     Debug.WriteLine($"[PedManager] Removed animDict '{animDict}' from memory.");
		// }
	}
}
