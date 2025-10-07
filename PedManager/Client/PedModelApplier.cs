using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PedManager.Client
{
	public interface IPedModelApplier
	{
		Task ApplyAsync(string modelName);
	}

	public sealed class PedModelApplier : IPedModelApplier
	{
		public async Task ApplyAsync(string modelName)
		{
			if (string.IsNullOrWhiteSpace(modelName))
			{
				Debug.WriteLine("[PedManager] Invalid model name.");
				return;
			}

			var hash = (uint)GetHashKey(modelName);

			if (!IsModelInCdimage(hash) || !IsModelValid(hash))
			{
				Debug.WriteLine($"[PedManager] Model '{modelName}' is not valid.");
				return;
			}

			RequestModel(hash);

			var timeoutAt = GetGameTimer() + 5000; // 5 seconds timeout
			while (!HasModelLoaded(hash))
			{
				await BaseScript.Delay(0);
				if (GetGameTimer() > timeoutAt)
				{
					Debug.WriteLine($"[PedManager] Timeout while loading model '{modelName}'.");
					SetModelAsNoLongerNeeded(hash);
					return;
				}
			}

			// Set player model removes all weapons and resets components
			SetPlayerModel(PlayerId(), hash);
			SetModelAsNoLongerNeeded(hash);
			SetPedDefaultComponentVariation(PlayerPedId());

			Debug.WriteLine($"[PedManager] Model '{modelName}' applied.");

			// Notify Armory to re-equip if available
			await BaseScript.Delay(100); // slight delay to ensure model is set
			BaseScript.TriggerServerEvent("Armory:Server:ReloadWeapons", GetPlayerServerId(PlayerId()).ToString());
		}

	}
	
	
}
