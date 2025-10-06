using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PlayerCore.Client
{
	public class PlayerCoreClient : BaseScript
	{
		private bool _sent;

		public PlayerCoreClient()
		{
			// Native FiveM event when player spawns
			EventHandlers["playerSpawned"] += new System.Action<dynamic>(OnPlayerSpawned);
			Tick += OnTick;
		}

		private async Task OnTick()
		{
			if (_sent) return;

			// If session is active, send ready event
			if (NetworkIsSessionActive())
			{
				_sent = true;
				TriggerServerEvent("PlayerCore:Server:PlayerReady");
			}

			await Task.FromResult(0);
		}

		// Triggered by FiveM when player spawns
		private void OnPlayerSpawned(dynamic _)
		{
			if (_sent) return;
			_sent = true;
			TriggerServerEvent("PlayerCore:Server:PlayerReady");
		}
	}
}