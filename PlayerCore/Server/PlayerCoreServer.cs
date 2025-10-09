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

		// Default player cords when no data in database
		private const float DefaultSpawnX = -1037.58f;
		private const float DefaultSpawnY = -2738.58f;
		private const float DefaultSpawnZ = 20.1693f;
		private const float DefaultSpawnHeading = 329.94f;

		public PlayerCoreServer()
		{
			_db = Exports["Database"];
			Debug.WriteLine("[PlayerCore] Server initialized.");

			EventHandlers["PlayerCore:Server:PlayerReady"] += new Action<Player>(OnPlayerReady);
			EventHandlers["PlayerCore:Server:SavePosition"] += new Action<Player, float, float, float, float>(OnSavePosition);
			EventHandlers["PlayerCore:Server:RequestDisconnect"] += new Action<Player>(OnRequestDisconnect);
			EventHandlers["PlayerCore:Server:PlayerDied"] += new Action<Player>(OnPlayerDied);
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

		// Handles player death event
		private void OnPlayerDied([FromSource] Player player)
		{
			if (player == null) return;
			Debug.WriteLine($"[PlayerCore] Player {player.Name} died;");
			TriggerEvent("Armory:Server:RemoveAllWeapons", player.Handle);
		}

		// Loads player data from database and applies to player state
		// Calls Client event to set spawn position after DB query loaded
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
				"SELECT money, job, pos_x, pos_y, pos_z, heading FROM players WHERE identifier = @identifier LIMIT 1",
				parameters,
				new Action<dynamic>(rows =>
				{
					int money = DefaultStartingMoney;
					string job = DefaultStartingJob;
					float posX = DefaultSpawnX;
					float posY = DefaultSpawnY;
					float posZ = DefaultSpawnZ;
					float heading = DefaultSpawnHeading;

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

							// Load position if valid
							if (row.pos_x != null) posX = Convert.ToSingle(row.pos_x);
							if (row.pos_y != null) posY = Convert.ToSingle(row.pos_y);
							if (row.pos_z != null) posZ = Convert.ToSingle(row.pos_z);
							if (row.heading != null) heading = Convert.ToSingle(row.heading);
						}
					}

					player.State.Set("money", money, true);
					player.State.Set("job", job, true);

					// Send spawn position to client after data from DB is loaded
					TriggerClientEvent(player, "PlayerCore:Client:SetSpawnPosition", posX, posY, posZ, heading);

					Debug.WriteLine($"[PlayerCore] Loaded data for player '{player.Name}' ({identifier}): Money={money}, Job={job}," +
						$"Position=({posX:F2}, {posY:F2}, {posZ:F2}");
					onLoaded?.Invoke();
				}));
		}

		// Saves player position to database (Execute UPDATE query)
		// Called by client event periodically and on specific events
		private void OnSavePosition([FromSource] Player player, float x, float y, float z, float heading)
		{
			if (player == null) return;

			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[PlayerCore] Could not resolve identifier.");
				return;
			}

			var parameters = new Dictionary<string, object>
			{
				{ "@identifier", identifier },
				{ "@pos_x", x },
				{ "@pos_y", y },
				{ "@pos_z", z },
				{ "@heading", heading }
			};

			_db.Query(
				"UPDATE players SET pos_x=@pos_x, pos_y=@pos_y, pos_z=@pos_z, heading=@heading WHERE identifier=@identifier",
				parameters,
				new Action<dynamic>(_ =>
				{
					Debug.WriteLine($"[PlayerCore] Saved position for player '{player.Name}' ({identifier}): X={x:F2}, Y={y:F2}, Z={z:F2}, Heading={heading:F2}");
				})
			);
		}



		//** HELPER METHODS **//

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
				{ "@job", DefaultStartingJob },
				{ "@pos_x", DefaultSpawnX },
				{ "@pos_y", DefaultSpawnY },
				{ "@pos_z", DefaultSpawnZ },
				{ "@heading", DefaultSpawnHeading }
			};

			_db.Insert(
				"INSERT INTO players (identifier, name, money, job, pos_x, pos_y, pos_z, heading, last_login) " +
				"VALUES (@identifier, @display_name, @money, @job, @pos_x, @pos_y, @pos_z, @heading, NOW()) " +
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

		private void OnRequestDisconnect([FromSource] Player player)
		{
			if (player == null) return;
			
			Debug.WriteLine($"[PlayerCore] Player {player.Name} manually logged out.");
			player.Drop("Logged out successfully.");
		}
	}
}