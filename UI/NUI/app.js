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
    } else if (data.action === 'openMenu') {
        console.log('[NUI] Opening generic menu:', data.type);
        showGenericMenu(data.type);
    } else if (data.action === 'closeMenu') {
        console.log('[NUI] Closing all menus');
        closeAllMenus();
    }
});

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
            const kind = type === "peds" ? "ped" : "weapon"; // normalize for server
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

function closeAllMenus() {
    console.log('[NUI] Closing all menus');
    const pedMenu = document.getElementById('ped-menu');
    const menu = document.getElementById('menu');
    if (pedMenu) pedMenu.style.display = 'none';
    if (menu) menu.style.display = 'none';

    const resourceName = getResourceName();
    fetch(`https://${resourceName}/closeMenu`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({})
    }).catch(err => console.error('[NUI] Error closing menu:', err));
}

// Close button handler
document.addEventListener('DOMContentLoaded', () => {
    console.log('[NUI] DOM loaded, setting up handlers');
    
    const closeBtn = document.getElementById('closePedMenu');
    if (closeBtn) {
        closeBtn.onclick = () => {
            console.log('[NUI] Close button clicked');
            closeAllMenus();
        };
    } else {
        console.warn('[NUI] Close button not found');
    }
});

// ESC key to close
document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
        const pedMenu = document.getElementById('ped-menu');
        const genericMenu = document.getElementById('menu');
        
        if ((pedMenu && pedMenu.style.display === 'block') || 
            (genericMenu && genericMenu.style.display === 'block')) {
            console.log('[NUI] ESC pressed, closing menus');
            event.preventDefault();
            closeAllMenus();
        }
    }
});

