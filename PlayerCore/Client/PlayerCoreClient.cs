using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using System;
using System.Collections.Generic;

namespace PlayerCore.Client
{
	public class PlayerCoreClient : BaseScript
	{
		private bool _sent;
		private bool _hasSpawnPosition;
		private float _spawnX, _spawnY, _spawnZ, _spawnHeading;

		// Auto-save interval in milliseconds 
		private const int AutoSaveInterval = 60000; // 60 seconds

		public PlayerCoreClient()
		{
			// Native FiveM event when player spawns
			EventHandlers["playerSpawned"] += new Action<dynamic>(OnPlayerSpawned);
			EventHandlers["PlayerCore:Client:SetSpawnPosition"] += new Action<float, float, float, float>(OnSetSpawnPosition);
			
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

		// Triggered by FiveM when player spawns
		private async void OnPlayerSpawned(dynamic _)
		{
			if (_sent) return;

			// Wait for player ped to be valid and network session to be active
			while (!NetworkIsSessionActive() || !IsPedAPlayer(PlayerPedId()) || PlayerPedId() <= 0)
			{
				await Delay(100);
			}

			_sent = true;
			TriggerServerEvent("PlayerCore:Server:PlayerReady");
			Debug.WriteLine("[PlayerCore] Player ready event sent to server.");

			// Apply spawn position if already received (handles race condition)
			if (_hasSpawnPosition)
			{
				await ApplySpawnPosition();
			}
		}

		// Receives spawn position from server after loading player data from DB or default
		private async void OnSetSpawnPosition(float x, float y, float z, float heading)
		{
			_spawnX = x;
			_spawnY = y;
			_spawnZ = z;
			_spawnHeading = heading;
			_hasSpawnPosition = true;

			Debug.WriteLine($"[PlayerCore] Received spawn position: X={x:F2}, Y={y:F2}, Z={z:F2}, Heading={heading:F2}");

			// If player already spawned, apply position immediately
			if (_sent)
			{
				await ApplySpawnPosition();
			}
		}

		// Applies spawn position to player
		private async Task ApplySpawnPosition()
		{
			var playerPed = PlayerPedId();

			while (!DoesEntityExist(playerPed))
			{
				await Delay(100);
				playerPed = PlayerPedId();
			}

			SetEntityCoords(playerPed, _spawnX, _spawnY, _spawnZ, false, false, false, true);
			SetEntityHeading(playerPed, _spawnHeading);

			Debug.WriteLine($"[PlayerCore] Applied spawn position: X={_spawnX:F2}, Y={_spawnY:F2}, Z={_spawnZ:F2}, Heading={_spawnHeading:F2}");
		}

		// Periodically saves the player's position to the server
		private async Task AutoSavePositionTick()
		{
			await Delay(AutoSaveInterval);

			// Only save if player is spawned and session is active
			if (!_sent || !NetworkIsSessionActive())
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