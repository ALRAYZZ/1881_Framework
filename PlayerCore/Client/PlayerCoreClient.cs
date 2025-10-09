using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using System;
using System.Collections.Generic;

namespace PlayerCore.Client
{
	public class PlayerCoreClient : BaseScript
	{
		// Auto-save interval in milliseconds 
		private const int AutoSaveInterval = 60000; // 60 seconds

		// Pending spawn data handshake
		private TaskCompletionSource<SpawnData> _spawnTcs;

		public PlayerCoreClient()
		{
			// Own the spawn flow fully via spawn manager
			Exports["spawnmanager"].setAutoSpawn(false);

			// Single auto spawn callback used for initial spawn and respawns
			Exports["spawnmanager"].setAutoSpawnCallback(new Action(async () =>
			{
				try
				{
					var data = await RequestSpawnDataAsync(5000);
					await SpawnWithManagerAsync(data);
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[PlayerCore] Error during auto-spawn: {ex}");

					// Fallback: spawn at current location with freemode ped
					// Potential REFACTOR 
					var fallback = CreateFallbackSpawnData();
					await SpawnWithManagerAsync(fallback);
				}
			}));

			// Enable auto-spawn and trigger initial spawn
			Exports["spawnmanager"].setAutoSpawn(true);
			Exports["spawnmanager"].forceRespawn();

			// Handle player dies
			EventHandlers["baseevents:onPlayerDied"] += new Action<int, dynamic>(OnPlayerDied);

			// Server sends spawn data to complete handshake
			EventHandlers["PlayerCore:Client:ReceiveSpawnData"] += new Action<float, float, float, float, string, int, int>(OnReceiveSpawnData);

			// Handle resource stop to save position on logout
			EventHandlers["onResourceStop"] += new Action<string>(OnResourceStop);
			
			// Start auto-save tick
			Tick += AutoSavePositionTick;

			// Register logout command
			RegisterCommand("logout", new Action<int, List<object>, string>((source, args, raw) =>
			{
				SaveCurrentPosition("Manual logout");
				_ = DelayedDisconnect();
			}), false);

			RegisterCommand("pos", new Action<int, List<object>, string>((source, args, raw) =>
			{
				var playerPed = PlayerPedId();
				if (DoesEntityExist(playerPed))
				{
					var coords = GetEntityCoords(playerPed, true);
					Debug.WriteLine($"[PlayerCore] Current position: X={coords.X:F2}, Y={coords.Y:F2}, Z={coords.Z:F2}");
				}
				else
				{
					Debug.WriteLine("[PlayerCore] Player ped doesn't exist.");
				}
			}), false);
		}

		// Struct to carry spawn data
		private struct SpawnData
		{
			public float X;
			public float Y;
			public float Z;
			public float Heading;
			public string PedModel;
			public int Health;
			public int Armor;
		}

		// Server -> Client: Final spawn data to use for the exact next spawn
		private void OnReceiveSpawnData(float  x, float y, float z, float heading, string pedModel, int health, int armor)
		{
			try
			{
				if (_spawnTcs != null && !_spawnTcs.Task.IsCompleted)
				{
					_spawnTcs.SetResult(new SpawnData
					{
						X = x,
						Y = y,
						Z = z,
						Heading = heading,
						PedModel = string.IsNullOrWhiteSpace(pedModel) ? "mp_m_freemode_01" : pedModel,
						Health = health,
						Armor = armor
					});
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[PlayerCore] Error in OnReceiveSpawnData: {ex}");
			}
		}

		// Ask server for spawn data and await response
		private async Task<SpawnData> RequestSpawnDataAsync(int timeoutMs)
		{
			_spawnTcs = new TaskCompletionSource<SpawnData>();

			// Request server to compute/return the next spawn point and model
			TriggerServerEvent("PlayerCore:Server:RequestSpawnData");

			var start = GetGameTimer();
			while (!_spawnTcs.Task.IsCompleted && (GetGameTimer() - start) < timeoutMs)
			{
				await Delay(0);
			}

			if (_spawnTcs.Task.IsCompleted)
			{
				return await _spawnTcs.Task;
			}

			Debug.WriteLine("[PlayerCore] Timeout waiting for spawn data from server.");
			return CreateFallbackSpawnData();
		}

		private SpawnData CreateFallbackSpawnData()
		{
			var ped = PlayerPedId();
			if (DoesEntityExist(ped))
			{
				var c = GetEntityCoords(ped, true);
				var h = GetEntityHeading(ped);
				return new SpawnData
				{
					X = c.X,
					Y = c.Y,
					Z = c.Z,
					Heading = h,
					PedModel = "mp_m_freemode_01",
					Health = 200,
					Armor = 0
				};
			}

			return new SpawnData
			{
				X = 0f,
				Y = 0f,
				Z = 72f,
				Heading = 0f,
				PedModel = "mp_m_freemode_01",
				Health = 200,
				Armor = 0
			};
		}

		// Peform spawn using spawnmanagert using server-provided data
		private async Task SpawnWithManagerAsync(SpawnData data)
		{
			// Options accepted by spawnmanager
			var options = new Dictionary<string, object>
			{
				["x"] = data.X,
				["y"] = data.Y,
				["z"] = data.Z,
				["heading"] = data.Heading,
				["model"] = data.PedModel,
				["skipFade"] = false
			};


			Exports["spawnmanager"].spawnPlayer(options, new Action(async () =>
			{
				try
				{
					var ped = PlayerPedId();

					// Basic state (do not change model here to avoid wiping weapons/props)
					if (data.Health > 0) SetEntityHealth(ped, data.Health);
					if (data.Armor > 0) SetPedArmour(ped, data.Armor);

					// Signal a single player spawn event
					// - PedManager listens and applies appearence(no model swap here)
					// - Armory listens and loads weapons
					TriggerEvent("PlayerCore:Client:PostSpawned", ped, data.X, data.Y, data.Z, data.Heading, data.PedModel);

					// Server can track spawns if needed
					TriggerServerEvent("PlayerCore:Server:OnSpawned");

					Debug.WriteLine($"[PlayerCore] Spawned at X={data.X:F2}, Y={data.Y:F2}, Z={data.Z:F2}, Heading={data.Heading:F2}");
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[PlayerCore] Error in post-spawn actions: {ex}");
				}
			}));

			await Task.FromResult(0);
		}

		// Death handling : keep notify server, spawnmanager will auto-respawn using callback
		private void OnPlayerDied(int killerId, dynamic deathData)
		{
			Debug.WriteLine("[PlayerCore] Player died, notifying server.");
			TriggerServerEvent("PlayerCore:Server:PlayerDied");
		}

		private async Task AutoSavePositionTick()
		{
			await Delay(AutoSaveInterval);

			// Only save if player is spawned and session is active
			if (!NetworkIsSessionActive())
			{
				return;
			}

			var playerPed = PlayerPedId();
			if (!DoesEntityExist(playerPed))
			{
				return;
			}

			SaveCurrentPosition("Auto-saved");
		}

		// Called when resource stops (player disconnecting)
		private void OnResourceStop(string resourceName)
		{
			if (resourceName != GetCurrentResourceName()) return;

			Debug.WriteLine("[PlayerCore] Resource stopping, saving position...");
			SaveCurrentPosition("Saved on logout");
			
			// Blocking delay to ensure server processes save before disconnect
			System.Threading.Thread.Sleep(200);
		}

		// Delayed disconnect after manual save
		private async Task DelayedDisconnect()
		{
			await Delay(500);
			Debug.WriteLine("[PlayerCore] Requesting disconnect after manual save...");
			TriggerServerEvent("PlayerCore:Server:RequestDisconnect");
		}

		// Helper method to save current position
		private void SaveCurrentPosition(string logMessage)
		{
			var playerPed = PlayerPedId();
			if (!DoesEntityExist(playerPed))
			{
				Debug.WriteLine("[PlayerCore] Player ped doesn't exist, skipping save.");
				return;
			}

			var coords = GetEntityCoords(playerPed, true);
			var heading = GetEntityHeading(playerPed);

			TriggerServerEvent("PlayerCore:Server:SavePosition", coords.X, coords.Y, coords.Z, heading);
			Debug.WriteLine($"[PlayerCore] {logMessage} position: X={coords.X:F2}, Y={coords.Y:F2}, Z={coords.Z:F2}, Heading={heading:F2}");
		}
	}
}