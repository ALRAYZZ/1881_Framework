using CitizenFX.Core;
using System;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

public class TestClient : BaseScript
{
	public TestClient()
	{
		Debug.WriteLine("[Test:Client] Initialized");

		// Auto-fire on load (after 5s delay)
		AutoFireAfterDelay();

		EventHandlers["test:pong"] += new Action<string>(msg =>
		{
			Debug.WriteLine($"[Test:Client] Pong: {msg}");
		});


		// Manual: Press F9 to fire (for isolation)
		Tick += async () =>
		{
			if (IsDisabledControlJustPressed(0, 56))  // F9
			{
				Debug.WriteLine("[Test:Client] Manual fire!");
				TriggerServerEvent("test:ping");
				await Delay(1000);  // Debounce
			}
			await Delay(0);
		};
	}

	private async void AutoFireAfterDelay()
	{
		await Delay(5000);
		Debug.WriteLine("[Test:Client] Auto-firing server event...");
		TriggerServerEvent("test:ping");
	}
}