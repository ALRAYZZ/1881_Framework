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
            EventHandlers["UI:OpenMenu"] += new Action<string>((menuType) =>
            {
                SendNuiMessage(Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    action = "openMenu",
                    type = menuType
                }));
            });

            EventHandlers["UI:OpenPedMenu"] += new Action<List<object>>((peds) =>
            {
                var pedNames = peds.Select(p => p.ToString()).ToList();

                // Call your NUI JS to populate menu
                SendNuiMessage(Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    action = "openPedMenu",
                    peds = pedNames
                }));
            });

			// Listen for NUI callbacks
            RegisterNuiCallbackType("selectItem");
            EventHandlers["__cfx_nui:selectItem"] += new Action<IDictionary<string, object>>((data) =>
            {
                if (data != null &&
                    data.TryGetValue("type", out var tVal) &&
                    data.TryGetValue("name", out var nVal))
                {
                    var type = tVal?.ToString() ?? string.Empty;
                    var name = nVal?.ToString() ?? string.Empty;
                    TriggerServerEvent("UI:SelectedItem", type, name);
                }
            });
		}

	}
}