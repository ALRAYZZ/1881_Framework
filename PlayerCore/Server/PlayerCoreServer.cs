using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PlayerCore.Server
{
	// 
	public class PlayerCoreServer : BaseScript
	{
		private readonly dynamic _db;

		private const int DefaultStartingMoney = 0;
		private const string DefaultStartingJob = "unemployed";

		private const string DefaultPedModel = "mp_m_freemode_01";

		// Hospital respawn cords
		private const float HospitalSpawnX = 307.43f;
		private const float HospitalSpawnY = -1433.14f;
		private const float HospitalSpawnZ = 29.97f;
		private const float HospitalSpawnHeading = 320.0f;

		// Default player cords when no data in database
		private const float DefaultSpawnX = -1037.58f;
		private const float DefaultSpawnY = -2738.58f;
		private const float DefaultSpawnZ = 20.1693f;
		private const float DefaultSpawnHeading = 329.94f;

		private readonly HashSet<string> _forceHospitalRespawn = new HashSet<string>();

		public PlayerCoreServer()
		{
			_db = Exports["Database"];
			Debug.WriteLine("[PlayerCore] Server initialized.");

			EventHandlers["PlayerCore:Server:PlayerReady"] += new Action<Player>(OnPlayerReady);
			EventHandlers["PlayerCore:Server:SavePosition"] += new Action<Player, float, float, float, float>(OnSavePosition);
			EventHandlers["PlayerCore:Server:RequestDisconnect"] += new Action<Player>(OnRequestDisconnect);

			// Respond to spawn data requests from PlayerCore client
			EventHandlers["PlayerCore:Server:RequestSpawnData"] += new Action<Player>(OnRequestSpawnData);

			// Mark next spawn to hospital on player death
			EventHandlers["PlayerCore:Server:PlayerDied"] += new Action<Player>(OnPlayerDied);

			// Note: no "SetPedModel" handling here; PedManager owns ped model updates
		}

		private void OnPlayerReady([FromSource] Player player)
		{
			if (player == null) return;

			EnsurePlayerRegistered(player, () =>
			{
				LoadPlayerData(player, () => { });
			});
		}

		private void OnPlayerDied([FromSource] Player player)
		{
			if (player == null) return;
			_forceHospitalRespawn.Add(player.Handle);
			Debug.WriteLine($"[PlayerCore] Player {player.Name} died; next spawn will be at hospital.");
		}

		private void LoadPlayerData(Player player, Action onLoaded)
		{
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
							firstRow = rows;
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

		private static string GetPreferredPedModel(Player player)
		{
			try
			{
				var m = player?.State?.Get("pedModel") as string;
				if (!string.IsNullOrWhiteSpace(m)) return m;
			}
			catch { }
			return DefaultPedModel;
		}

		private void OnRequestSpawnData([FromSource] Player player)
		{
			if (player == null) return;

			var pedModel = GetPreferredPedModel(player);
			int health = 200;
			int armor = 0;

			if (_forceHospitalRespawn.Remove(player.Handle))
			{
				TriggerClientEvent(player, "PlayerCore:Client:ReceiveSpawnData",
					HospitalSpawnX, HospitalSpawnY, HospitalSpawnZ, HospitalSpawnHeading,
					pedModel, health, armor);

				Debug.WriteLine($"[PlayerCore] OnRequestSpawnData: forced hospital respawn for {player.Name} with ped '{pedModel}'.");
				return;
			}

			var identifier = GetStableIdentifier(player);
			if (string.IsNullOrWhiteSpace(identifier))
			{
				Debug.WriteLine("[PlayerCore] OnRequestSpawnData: missing identifier, sending defaults.");
				TriggerClientEvent(player, "PlayerCore:Client:ReceiveSpawnData",
					DefaultSpawnX, DefaultSpawnY, DefaultSpawnZ, DefaultSpawnHeading,
					pedModel, health, armor);
				return;
			}

			var parameters = new Dictionary<string, object> { { "@identifier", identifier } };

			_db.Query(
				"SELECT pos_x, pos_y, pos_z, heading FROM players WHERE identifier = @identifier LIMIT 1",
				parameters,
				new Action<dynamic>(rows =>
				{
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
							if (enumerator.MoveNext()) firstRow = enumerator.Current;
						}
						else
						{
							firstRow = rows;
						}

						if (firstRow != null)
						{
							dynamic row = firstRow;
							if (row.pos_x != null) posX = Convert.ToSingle(row.pos_x);
							if (row.pos_y != null) posY = Convert.ToSingle(row.pos_y);
							if (row.pos_z != null) posZ = Convert.ToSingle(row.pos_z);
							if (row.heading != null) heading = Convert.ToSingle(row.heading);
						}
					}

					TriggerClientEvent(player, "PlayerCore:Client:ReceiveSpawnData",
						posX, posY, posZ, heading, pedModel, health, armor);

					Debug.WriteLine($"[PlayerCore] Sent spawn data to {player.Name}: X={posX:F2}, Y={posY:F2}, Z={posZ:F2}, H={heading:F2}, Model={pedModel}");
				})
			);
		}

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

		private async void OnRequestDisconnect([FromSource] Player player)
		{
			if (player == null) return;

			try
			{
				TriggerEvent("Armory:Server:PersistWeaponsNow", player.Handle);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[PlayerCore] Error while saving weapons for player {player.Name}: {ex.Message}");
			}

			await Delay(250);

			Debug.WriteLine($"[PlayerCore] Player {player.Name} manually logged out.");
			player.Drop("Logged out successfully.");
		}
	}
}