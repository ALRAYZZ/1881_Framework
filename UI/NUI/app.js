// Helper function to get the resource name without shadowing FiveM's built-in
function getResourceName() {
    if (typeof window.GetParentResourceName === 'function') {
        return window.GetParentResourceName(); // FiveM-provided
    }
    // Fallback: try to parse from path
    if (window.location.hostname === '') {
        const pathParts = window.location.pathname.split('/');
        const resourceIndex = pathParts.findIndex(part => part.toLowerCase() === 'ui');
        if (resourceIndex !== -1) {
            return pathParts[resourceIndex];
        }
    }
    return 'UI';
}

// Listen for messages from C#
window.addEventListener('message', (event) => {
    const data = event.data;

    console.log('[NUI] Received message:', data);

    if (data.action === 'openPedMenu') {
        console.log('[NUI] Opening ped menu with peds:', data.peds);
        showPedMenu(data.peds);
    } else if (data.action === 'openWeaponMenu') {
        console.log('[NUI] Opening weapon menu with weapons:', data.weapons);
        showWeaponMenu(data.weapons);
    } else if (data.action === 'openMenu') {
        console.log('[NUI] Opening generic menu:', data.type);
        showGenericMenu(data.type);
    } else if (data.action === 'closeMenu') {
        console.log('[NUI] Closing all menus');
        closeAllMenus();
    }
});

/* Ped Menu Functions */
function showPedMenu(pedList) {
    console.log('[NUI] showPedMenu called with:', pedList);

    const pedMenu = document.getElementById('ped-menu');
    const container = document.getElementById('pedList');

    // Clear previous items
    container.innerHTML = '';

    // Populate the list
    pedList.forEach(ped => {
        const item = document.createElement('li');
        item.innerText = ped;
        item.className = 'ped-item';
        item.onclick = () => selectPed(ped);
        container.appendChild(item);
    });

    // Show the menu
    pedMenu.style.display = 'block';
    console.log('[NUI] Ped menu displayed');
}

function selectPed(pedName) {
    console.log('[NUI] Ped selected:', pedName);
    closeAllMenus();
    const resourceName = getResourceName();
    fetch(`https://${resourceName}/selectItem`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ type: 'ped', name: pedName })
    }).then(resp => resp.json()).then(data => {
        console.log('[NUI] Server response:', data);
    }).catch(err => console.error('[NUI] Error sending selection:', err));
}

/* Weapon Menu Functions */
function showWeaponMenu(weaponList) {
    console.log('[NUI] showWeaponMenu called with:', weaponList);

    if (!weaponList || weaponList.length === 0) {
        console.error('[NUI] ERROR: No weapons received!');
        return;
    }

    const weaponMenu = document.getElementById('weapon-menu');
    const container = document.getElementById('weaponList');

    // Clear previous items
    container.innerHTML = '';

    // Populate the list
    weaponList.forEach(weapon => {
        const item = document.createElement('li');
        item.innerText = weapon;
        item.className = 'weapon-item';
        item.onclick = () => selectWeapon(weapon);
        container.appendChild(item);
    });

    // Show the menu
    weaponMenu.style.display = 'block';
    console.log('[NUI] Weapon menu displayed with', weaponList.length, 'weapons');
}

function selectWeapon(weaponName) {
    console.log('[NUI] Weapon selected:', weaponName);
    closeAllMenus();
    const resourceName = getResourceName();
    fetch(`https://${resourceName}/selectItem`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ type: 'weapon', name: weaponName })
    }).then(resp => resp.json()).then(data => {
        console.log('[NUI] Server response:', data);
    }).catch(err => console.error('[NUI] Error sending selection:', err));
}

/* Generic Menu Function */
function showGenericMenu(type) {
    const menu = document.getElementById("menu");
    const title = document.getElementById("menu-title");
    const list = document.getElementById("menu-list");

    title.textContent = type === "peds" ? "Select a Ped" : "Select a Weapon";
    list.innerHTML = "";

    const items = type === "peds" ? ["g_m_m_chiboss_01", "s_m_m_security_01"] : ["WEAPON_PISTOL", "WEAPON_KNIFE"];
    items.forEach(i => {
        const li = document.createElement("li");
        li.textContent = i;
        li.onclick = () => {
            closeAllMenus();
            const resourceName = getResourceName();
            const kind = type === "peds" ? "ped" : "weapon";
            fetch(`https://${resourceName}/selectItem`, {
                method: "POST",
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ type: kind, name: i })
            }).catch(err => console.error('[NUI] Error:', err));
        };
        list.appendChild(li);
    });

    menu.style.display = "block";
}

/* Close All Menus */
function closeAllMenus() {
    console.log('[NUI] Closing all menus');
    const pedMenu = document.getElementById('ped-menu');
    const weaponMenu = document.getElementById('weapon-menu');
    const menu = document.getElementById('menu');

    if (pedMenu) pedMenu.style.display = 'none';
    if (weaponMenu) weaponMenu.style.display = 'none';
    if (menu) menu.style.display = 'none';

    const resourceName = getResourceName();
    fetch(`https://${resourceName}/closeMenu`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({})
    }).catch(err => console.error('[NUI] Error closing menu:', err));
}

/* Event Handlers */
document.addEventListener('DOMContentLoaded', () => {
    console.log('[NUI] DOM loaded, setting up handlers');

    // Ped menu close button
    const closePedBtn = document.getElementById('closePedMenu');
    if (closePedBtn) {
        closePedBtn.onclick = () => {
            console.log('[NUI] Close ped menu button clicked');
            closeAllMenus();
        };
    }

    // Weapon menu close button
    const closeWeaponBtn = document.getElementById('closeWeaponMenu');
    if (closeWeaponBtn) {
        closeWeaponBtn.onclick = () => {
            console.log('[NUI] Close weapon menu button clicked');
            closeAllMenus();
        };
    }
});

// ESC key to close any open menu
document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
        const pedMenu = document.getElementById('ped-menu');
        const weaponMenu = document.getElementById('weapon-menu');
        const genericMenu = document.getElementById('menu');

        if ((pedMenu && pedMenu.style.display === 'block') ||
            (weaponMenu && weaponMenu.style.display === 'block') ||
            (genericMenu && genericMenu.style.display === 'block')) {
            console.log('[NUI] ESC pressed, closing menus');
            event.preventDefault();
            closeAllMenus();
        }
    }
});