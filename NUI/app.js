window.addEventListener('message', (event) => {
    const data = event.data;
    if (data.action === "openMenu") {
        const menu = document.getElementById("menu");
        const title = document.getElementById("menu-title");
        const list = document.getElementById("menu-list");

        title.textContent = data.type === "peds" ? "Select a Ped" : "Select a Weapon";
        list.innerHTML = "";

        const items = data.type === "peds" ? ["g_m_m_chiboss_01", "s_m_m_security_01"] : ["WEAPON_PISTOL", "WEAPON_KNIFE"];
        items.forEach(i => {
            const li = document.createElement("li");
            li.textContent = i;
            li.onclick = () => {
                fetch(`https://${GetParentResourceName()}/selectItem`, {
                    method: "POST",
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ type: data.type, name: i })
                });
            };
            list.appendChild(li);
        });

        menu.style.display = "block";
    }
});
window.addEventListener('message', (event) => {
    const data = event.data;
    if (data.action === 'openPedMenu') {
        const pedList = data.peds; // array of ped names
        showPedMenu(pedList); // your function to populate the UI
    }
});

function showPedMenu(pedList) {
    const container = document.getElementById("pedList");
    container.innerHTML = '';
    pedList.forEach(ped => {
        const item = document.createElement('div');
        item.innerText = ped;
        item.onclick = () => selectPed(ped);
        container.appendChild(item);
    });
}

function selectPed(pedName) {
    fetch(`https://${GetParentResourceName()}/pedSelected`, {
        method: 'POST',
        body: JSON.stringify({ ped: pedName })
    });
}

