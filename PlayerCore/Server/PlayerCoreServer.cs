using System;
using System.Collections.Generic;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PlayerCore.Server
{
	// 
	public class PlayerCoreServer : BaseScript
	{
		private readonly dynamic _db;

		// Defaults for new players; adjust as needed
		private const int DefaultStartingMoney = 0;
		private const string DefaultStartingJob = "unemployed";

		public PlayerCoreServer()
		{
			_db = Exports["Database"];
			Debug.WriteLine("[PlayerCore] Server initialized.");

			EventHandlers["PlayerCore:Server:PlayerReady"] += new Action<Player>(OnPlayerReady);
		}

		// Receives player ready event from client
		// We need [FromSource] when clients trigger server events and we need the player object else player is null
		private void OnPlayerReady([FromSource] Player player)
		{
			if (player == null) return;

			EnsurePlayerRegistered(player, () =>
			{
				// Calls the method to load player data from database
				LoadPlayerData(player, () =>
				{
					// Calls PedManager to apply initial ped model on spawn
					TriggerEvent("PedManager:Server:ApplyInitialPed", player.Handle);
					// Load weapons from Armory module
					TriggerEvent("Armory:Server:LoadWeapons", player.Handle);
				});
			});
		}
		// Loads player data from database and applies to player state
		private void LoadPlayerData(Player player, Action onLoaded)
		{
			// Gets player identifier (license2 preferred)
			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[PlayerCore] Could not resolve stable identifier; skipping data load.");
				onLoaded?.Invoke();
				return;
			}

			var parameters = new Dictionary<string, object> { { "@identifier", identifier } };

			_db.Query(
				"SELECT money, job FROM players WHERE identifier = @identifier LIMIT 1",
				parameters,
				new Action<dynamic>(rows =>
				{
					int money = DefaultStartingMoney;
					string job = DefaultStartingJob;

					if (rows != null)
					{
						dynamic firstRow = null;
						if (rows is System.Collections.IEnumerable enumerable)
						{
							var enumerator = enumerable.GetEnumerator();
							if (enumerator.MoveNext())
							{
								firstRow = enumerator.Current;
							}
						}
						else
						{
							firstRow = rows; // Fallback if single object
						}

						if (firstRow != null)
						{
							dynamic row = firstRow;
							if (row.money != null) money = Convert.ToInt32(row.money);
							if (row.job != null) job = Convert.ToString(row.job);
						}
					}

					player.State.Set("money", money, true);
					player.State.Set("job", job, true);

					Debug.WriteLine($"[PlayerCore] Loaded data for player '{player.Name}' ({identifier}): Money={money}, Job={job}");
					onLoaded?.Invoke();
				}));
		}


		// Ensures the player is registered in the database; if not, creates a new record
		private void EnsurePlayerRegistered(Player player, Action onDone)
		{
			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[PlayerCore] Could not resolve stable identifier; skipping registration.");
				onDone?.Invoke();
				return;
			}

			var displayName = player.Name ?? GetPlayerName(player.Handle) ?? "Unknown";
			if (displayName.Length > 50) displayName = displayName.Substring(0, 50);

			var p = new Dictionary<string, object>
			{
				{ "@identifier", identifier },
				{ "@display_name", displayName },
				{ "@money", DefaultStartingMoney },
				{ "@job", DefaultStartingJob }
			};

			_db.Insert(
				"INSERT INTO players (identifier, name, money, job, last_login) " +
				"VALUES (@identifier, @display_name, @money, @job, NOW()) " +
				"ON DUPLICATE KEY UPDATE " +
				"name=@display_name, last_login=NOW()",
				p,
				new Action<dynamic>(_ =>
				{
					Debug.WriteLine($"[PlayerCore] Registered/updated player '{displayName}' ({identifier})");
					onDone?.Invoke();
				})
			);
		}
		// Gets the most stable identifier available for a player
		private static string GetStableIdentifier(Player player)
		{
			if (player == null) return null;

			string license = null, fallback = null;
			var count = GetNumPlayerIdentifiers(player.Handle);
			for (int i = 0; i < count; i++)
			{
				var id = GetPlayerIdentifier(player.Handle, i);
				if (string.IsNullOrEmpty(id)) continue;

				if (id.StartsWith("license2:", StringComparison.OrdinalIgnoreCase)) return id;
				if (id.StartsWith("license:", StringComparison.OrdinalIgnoreCase) && license == null) license = id;
				if (fallback == null) fallback = id;
			}
			return license ?? fallback;
		}
	}
}