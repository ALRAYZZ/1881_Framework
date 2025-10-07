using CitizenFX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CitizenFX.Core.Native.API;

namespace UI.Client
{
    public class UIClient : BaseScript
    {
        public UIClient()
        {
            Debug.WriteLine("[UI:Client] UIClient initialized");

            EventHandlers["UI:OpenMenu"] += new Action<string>((menuType) =>
            {
                Debug.WriteLine($"[UI:Client] UI:OpenMenu triggered with type: {menuType}");
                
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    action = "openMenu",
                    type = menuType
                });
                
                Debug.WriteLine($"[UI:Client] Sending NUI message: {json}");
                SetNuiFocus(true, true);
                SendNuiMessage(json);
            });

            EventHandlers["UI:OpenPedMenu"] += new Action<List<object>>((peds) =>
            {
                Debug.WriteLine($"[UI:Client] UI:OpenPedMenu triggered with {peds?.Count ?? 0} peds");
                
                if (peds == null || peds.Count == 0)
                {
                    Debug.WriteLine("[UI:Client] WARNING: No peds received!");
                    return;
                }

                var pedNames = peds.Select(p => p.ToString()).ToList();
                Debug.WriteLine($"[UI:Client] Ped names: {string.Join(", ", pedNames)}");

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    action = "openPedMenu",
                    peds = pedNames
                });

                Debug.WriteLine($"[UI:Client] Sending NUI message: {json}");
                SetNuiFocus(true, true);
                SendNuiMessage(json);
            });

            // Listen for NUI callbacks
            RegisterNuiCallbackType("selectItem");
            EventHandlers["__cfx_nui:selectItem"] += new Action<IDictionary<string, object>, CallbackDelegate>((data, cb) =>
            {
                Debug.WriteLine("[UI:Client] selectItem callback received");
                
                // Disable NUI focus immediately to restore game control
                SetNuiFocus(false, false);
                
                if (data != null &&
                    data.TryGetValue("type", out var tVal) &&
                    data.TryGetValue("name", out var nVal))
                {
                    var type = tVal?.ToString() ?? string.Empty;
                    var name = nVal?.ToString() ?? string.Empty;
                    
                    Debug.WriteLine($"[UI:Client] Selected: type={type}, name={name}");
                    
                    try
                    {
						// Send selection to server
                        Debug.WriteLine($"[UI:Client] Calling TriggerServerEvent with type={type}, name={name}");
						TriggerServerEvent("UI:SelectedItem", type, name);
                        Debug.WriteLine($"[UI:Client] TriggerServerEvent called successfully");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[UI:Client] ERROR calling TriggerServerEvent: {ex.Message}");
                    }
                    
                    cb(new { status = "ok" });
                }
                else
                {
                    Debug.WriteLine("[UI:Client] WARNING: Invalid data in selectItem callback");
                    cb(new { status = "error" });
                }
            });

            // Close menu callback
            RegisterNuiCallbackType("closeMenu");
            EventHandlers["__cfx_nui:closeMenu"] += new Action<IDictionary<string, object>, CallbackDelegate>((data, cb) =>
            {
                Debug.WriteLine("[UI:Client] closeMenu callback received");
                
                // Restore game control
                SetNuiFocus(false, false);
                
                cb(new { status = "ok" });
            });

			Debug.WriteLine("[UI:Client] All event handlers registered successfully");
        }
    }
}