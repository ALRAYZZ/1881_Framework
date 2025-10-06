using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace PedManager.Server
{
    public sealed class PedService : IPedService
    {
        private readonly dynamic _db;

        public PedService(dynamic db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public List<string> GetAllAvailablePeds()
        {
            // This can be loaded from a config or DB in a more complex implementation
            return new List<string>
            {
                "g_m_m_chiboss_01",
                "g_m_m_chicold_01",
                "a_m_m_skater_01",
		        "s_m_m_ciasec_01",
		        "mp_m_freemode_01"
			};
		}

		// Gets the identifier from player on login; prefers license2, then license, then first available
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

        // Default: persist to DB (used by /setped)
        public void SetPedFor(Player target, string modelName)
            => SetPedFor(target, modelName, true);

        // Apply ped with no write on DB; optionally persist to DB (used during login apply)
        public void SetPedFor(Player target, string modelName, bool persist)
        {
            if (target == null || string.IsNullOrWhiteSpace(modelName)) return;

            target.TriggerEvent("PedManager:Client:SetPed", modelName);

            if (!persist) return; // don't save during login apply

            var identifier = GetStableIdentifier(target);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                Debug.WriteLine("[PedManager] Could not resolve stable identifier; skipping save.");
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                { "@identifier", identifier },
                { "@outfit_name", "default" },
                { "@ped_model", modelName },
                { "@outfit_data", "{}" }
            };

            _db.Insert(
                "INSERT INTO player_outfits (identifier, outfit_name, ped_model, outfit_data, date_saved) " +
                "VALUES (@identifier, @outfit_name, @ped_model, @outfit_data, NOW()) " +
                "ON DUPLICATE KEY UPDATE ped_model=VALUES(ped_model), outfit_data=VALUES(outfit_data), date_saved=NOW()",
                parameters,
                new Action<dynamic>((id) =>
                {
                    Debug.WriteLine($"[PedManager] Saved ped for {identifier}, id: {id}");
                })
            );
        }

        // Load ped from DB (by prioritized identifiers) or persist a default, then apply without persisting again.
        public void ApplyInitialPedFor(Player player)
        {
            if (player == null) return;

            var stableId = GetStableIdentifier(player);
            if (string.IsNullOrWhiteSpace(stableId))
            {
                Debug.WriteLine("[PedManager] Missing stable identifier for ped load.");
                return;
            }

            // Collect identifiers in priority order
            string license = null, license2 = null, steam = null, discord = null, fivem = null;
            var count = GetNumPlayerIdentifiers(player.Handle);
            for (int i = 0; i < count; i++)
            {
                var id = GetPlayerIdentifier(player.Handle, i) ?? "";
                if (id.StartsWith("license2:", StringComparison.OrdinalIgnoreCase)) license2 = id;
                else if (id.StartsWith("license:", StringComparison.OrdinalIgnoreCase)) license = id;
                else if (id.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)) steam = id;
                else if (id.StartsWith("discord:", StringComparison.OrdinalIgnoreCase)) discord = id;
                else if (id.StartsWith("fivem:", StringComparison.OrdinalIgnoreCase)) fivem = id;
            }

            var idPriority = new List<string>();
            if (!string.IsNullOrEmpty(license2)) idPriority.Add(license2);
            if (!string.IsNullOrEmpty(license)) idPriority.Add(license);
            if (!string.IsNullOrEmpty(steam)) idPriority.Add(steam);
            if (!string.IsNullOrEmpty(discord)) idPriority.Add(discord);
            if (!string.IsNullOrEmpty(fivem)) idPriority.Add(fivem);
            if (!idPriority.Contains(stableId)) idPriority.Add(stableId);

            const string outfitName = "default";

            var placeholders = string.Join(", ", Enumerable.Range(0, idPriority.Count).Select(i => $"@id{i}"));
            var param = new Dictionary<string, object> { { "@outfit_name", outfitName } };
            for (int i = 0; i < idPriority.Count; i++)
                param[$"@id{i}"] = idPriority[i];

            var sql =
                $"SELECT ped_model " +
                $"FROM player_outfits " +
                $"WHERE outfit_name=@outfit_name AND identifier IN ({placeholders}) " +
                $"ORDER BY FIELD(identifier, {placeholders}) " +
                $"LIMIT 1";

            Debug.WriteLine($"[PedManager] Resolving ped for [{string.Join(", ", idPriority)}], outfit='{outfitName}'");

            _db.Scalar(
                sql,
                param,
                new Action<dynamic>((pedObj) =>
                {
                    string pedModel = null;
                    if (pedObj is string s) pedModel = s;
                    else if (pedObj != null) pedModel = pedObj.ToString();

                    var loadedFromDb = !string.IsNullOrWhiteSpace(pedModel);
                    if (!loadedFromDb)
                    {
                        pedModel = "g_m_m_chiboss_01"; // default male

                        var insertParams = new Dictionary<string, object>
                        {
                            { "@identifier", idPriority[0] },  // Use primary (license2 if present)
                            { "@outfit_name", outfitName },
                            { "@ped_model", pedModel },
                            { "@outfit_data", "{}" }
                        };

                        _db.Insert(
                            "INSERT INTO player_outfits (identifier, outfit_name, ped_model, outfit_data, date_saved) " +
                            "VALUES (@identifier, @outfit_name, @ped_model, @outfit_data, NOW()) " +
                            "ON DUPLICATE KEY UPDATE ped_model = @ped_model, outfit_data = @outfit_data, date_saved = NOW()",
                            insertParams,
                            new Action<dynamic>(_ => { })
                        );

                        Debug.WriteLine($"[PedManager] No stored ped found. Persisted default for {idPriority[0]}.");
                    }

                    // Apply without persisting again
                    SetPedFor(player, pedModel, false);
                    Debug.WriteLine($"[PedManager] {(loadedFromDb ? "Loaded" : "Applied default")} ped '{pedModel}' for {stableId} ({player.Handle})");
                })
            );
        }
    }
}
