using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using System.Linq;
using System;

public class TestServer : BaseScript
{
	public TestServer()
	{
		Debug.WriteLine("[Test:Server] Initialized");

		EventHandlers["test:ping"] += new Action<Player>(OnPing);
	}

	private void OnPing([FromSource] Player player)
	{
		Debug.WriteLine($"[Test:Server] Ping received from {player.Name} (ID: {player.Handle})!");

		// Echo back to all clients to verify full loop
		Players.ToList().ForEach(p => TriggerClientEvent(p, "test:pong", $"Pong from server to {p.Name}!"));
	}
}