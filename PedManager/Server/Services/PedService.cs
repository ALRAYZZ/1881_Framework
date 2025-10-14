using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using Newtonsoft.Json;
using PedManager.Server.Interfaces;

namespace PedManager.Server.Services
{
    public sealed class PedService : IPedService
    {
        private readonly dynamic _db;

        // Cache the ped list, don’t re-read the file every time
        private static List<string> _cachedPeds;

        public PedService(dynamic db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public List<string> GetAllAvailablePeds()
        {
            // Return cached list if already loaded
            if (_cachedPeds != null && _cachedPeds.Count > 0)
                return _cachedPeds;

            try
            {
                var resource = GetCurrentResourceName();
                var json = LoadResourceFile(resource, "data/peds.json");

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var fromFile = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                    // Normalize: trim, distinct, filter empty, sort (case-insensitive)
                    _cachedPeds = fromFile
                        .Select(s => (s ?? string.Empty).Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (_cachedPeds.Count > 0)
                    {
                        Debug.WriteLine($"[PedManager] Loaded {_cachedPeds.Count} peds from data/peds.json");
                        return _cachedPeds;
                    }
                }

                Debug.WriteLine("[PedManager] data/peds.json missing or empty; using fallback ped list.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PedManager] Failed to load peds.json: {ex.Message}");
            }

            // Safe fallback list 
            _cachedPeds = new List<string>
            {
                "mp_m_freemode_01",
                "mp_f_freemode_01",

                // Civilians (female)
                "a_f_m_bevhills_01","a_f_m_bevhills_02",
                "a_f_y_bevhills_01","a_f_y_bevhills_02",
                "a_f_y_vinewood_01","a_f_y_vinewood_02","a_f_y_vinewood_03","a_f_y_vinewood_04",
                "a_f_y_hipster_01","a_f_y_hipster_02","a_f_y_hipster_03","a_f_y_hipster_04",
                "a_f_y_business_01","a_f_y_business_02","a_f_y_business_03","a_f_y_business_04",
                "a_f_y_runner_01","a_f_y_golfer_01","a_f_y_smartcas_01","a_f_y_tourist_01","a_f_y_hiker_01",
                "a_f_y_beach_01","a_f_y_beach_02","a_f_y_beach_03",
                "a_f_y_eastsa_01","a_f_y_eastsa_02","a_f_y_eastsa_03",
                "a_f_m_soucent_01","a_f_m_soucent_02","a_f_m_soucentmc_01",
                "a_f_m_skidrow_01","a_f_m_tramp_01",
                "a_f_m_fatwhite_01","a_f_m_fatbla_01","a_f_m_fatcult_01",

                // Civilians (male)
                "a_m_m_skater_01","a_m_y_skater_01","a_m_y_skater_02",
                "a_m_y_runner_01","a_m_y_golfer_01",
                "a_m_y_business_01","a_m_y_business_02","a_m_y_business_03",
                "a_m_y_beach_01","a_m_y_beach_02","a_m_y_beach_03",
                "a_m_y_hipster_01","a_m_y_hipster_02","a_m_y_hipster_03","a_m_y_hipster_04",
                "a_m_y_vinewood_01","a_m_y_vinewood_02","a_m_y_vinewood_03","a_m_y_vinewood_04",
                "a_m_m_business_01","a_m_m_business_02","a_m_m_business_03",
                "a_m_m_eastsa_01","a_m_m_eastsa_02","a_m_m_eastsa_03",
                "a_m_y_eastsa_01","a_m_y_eastsa_02",
                "a_m_m_soucent_01","a_m_m_soucent_02","a_m_m_soucent_03",
                "a_m_y_soucent_01","a_m_y_soucent_02","a_m_y_soucent_03",
                "a_m_m_salton_01","a_m_m_salton_02","a_m_m_salton_03","a_m_m_salton_04",
                "a_m_y_salton_01","a_m_o_salton_01",
                "a_m_m_tramp_01","a_m_m_trampbeac_01",
                "a_m_m_hillbilly_01","a_m_m_hillbilly_02",
                "a_m_m_genfat_01","a_m_m_genfat_02","a_m_m_fatlatin_01",

                // Services / law / emergency
                "s_m_y_cop_01","s_m_y_hwaycop_01","s_m_y_sheriff_01",
                "s_m_y_swat_01","s_m_m_prisguard_01",
                "s_m_m_paramedic_01","s_m_m_doctor_01",
                "s_m_m_fireman_01",
                "s_m_m_postal_01","s_m_m_postal_02",
                "s_m_y_construct_01","s_m_y_construct_02","s_m_m_construc_01","s_m_m_construc_02",
                "s_m_m_security_01","s_m_m_highsec_01","s_m_m_highsec_02","s_m_m_fibsec_01","s_m_m_ciasec_01",
                "s_m_m_marine_01","s_m_m_marine_02","s_m_y_marine_01","s_m_y_marine_02","s_m_y_marine_03",
                "s_m_y_xmech_01","s_m_y_xmech_02_mp",
                "s_m_y_airworker","s_m_m_pilot_01","s_m_y_pilot_01",
                "s_m_m_ammucountry",
                "s_f_y_bartender_01","s_m_m_bouncer_01",
                "s_f_m_maid_01","s_f_m_shop_high","s_f_y_shop_mid","s_f_y_shop_low","s_m_m_shop_mask",
                "s_f_y_baywatch_01","s_m_y_baywatch_01","s_f_y_scrubs_01",

                // Gangs
                "g_m_y_ballaeast_01","g_m_y_ballaorig_01","g_m_y_ballasout_01",
                "g_m_y_famca_01","g_m_y_famdnf_01","g_m_y_famfor_01",
                "g_m_y_lost_01","g_m_y_lost_02","g_m_y_lost_03",
                "g_m_y_mexgoon_01","g_m_y_mexgoon_02","g_m_y_mexgoon_03","g_m_m_mexboss_01","g_m_m_mexboss_02",
                "g_m_y_salvagoon_01","g_m_y_salvagoon_02","g_m_y_salvagoon_03","g_m_m_salvaboss_01",
                "g_m_y_korean_01","g_m_y_korean_02","g_m_m_korboss_01",
                "g_m_m_armboss_01","g_m_m_armlieut_01",
                "g_m_m_chiboss_01","g_m_m_chicold_01","g_m_y_chigoon_01","g_m_y_chigoon_02","g_m_y_chigoon_03",
                "g_m_y_strpunk_01","g_m_y_strpunk_02",

                // Story characters (safe/common)
                "ig_franklin","ig_michael","ig_trevor","ig_lamardavis","ig_lestercrest"
            };

            return _cachedPeds;
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

        // Default: persist to DB (used by /setped)
        public void SetPedFor(Player target, string modelName)
            => SetPedFor(target, modelName, true);

        // Apply ped with no write on DB; optionally persist to DB (used during login apply)
        public void SetPedFor(Player target, string modelName, bool persist)
        {
            if (target == null || string.IsNullOrWhiteSpace(modelName)) return;

            var allowed = GetAllAvailablePeds();
            var resolved = allowed.FirstOrDefault(m => string.Equals(m, modelName, StringComparison.OrdinalIgnoreCase));
            if (resolved == null)
            {
                Debug.WriteLine($"[PedManager] '{modelName}' not in allowed ped list. Falling back to default.");
                resolved = allowed[0];
            }

            // Update server-side state so other systems (PlayerCore) can read it for spawn
            try { target.State.Set("pedModel", resolved, true); } catch { /* ignore */ }

            // Apply on client
            target.TriggerEvent("PedManager:Client:SetPed", resolved);

            if (!persist) return;

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
                { "@ped_model", resolved },
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

        // Load ped from DB or persist a default, then apply without persisting again.
        public void ApplyInitialPedFor(Player player)
        {
            if (player == null) return;

            var stableId = GetStableIdentifier(player);
            if (string.IsNullOrWhiteSpace(stableId))
            {
                Debug.WriteLine("[PedManager] Missing stable identifier for ped load.");
                return;
            }

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

                    var allowed = GetAllAvailablePeds();
                    var loadedFromDb = !string.IsNullOrWhiteSpace(pedModel) &&
                                       allowed.Any(m => string.Equals(m, pedModel, StringComparison.OrdinalIgnoreCase));

                    if (!loadedFromDb)
                    {
                        pedModel = allowed[0];

                        var insertParams = new Dictionary<string, object>
                        {
                            { "@identifier", idPriority[0] },
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

                        Debug.WriteLine($"[PedManager] No valid stored ped found. Persisted default for {idPriority[0]}.");
                    }

                    // Ensure state is set so PlayerCore can read it for spawn
                    try { player.State.Set("pedModel", pedModel, true); } catch { /* ignore */ }

                    // Apply without re-persisting
                    SetPedFor(player, pedModel, false);
                    Debug.WriteLine($"[PedManager] {(loadedFromDb ? "Loaded" : "Applied default")} ped '{pedModel}' for {stableId} ({player.Handle})");
                })
            );
        }
    }
}
