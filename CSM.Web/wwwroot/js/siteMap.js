window.siteMap = (function () {

    let map;
    let marker;
    let dotnet;

    async function loadLeaflet() {

        if (window.L)
            return;

        if (!document.getElementById("leaflet-css")) {
            const css = document.createElement("link");
            css.id = "leaflet-css";
            css.rel = "stylesheet";
            css.href = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.css";
            document.head.appendChild(css);
        }

        await new Promise((resolve, reject) => {

            if (window.L) {
                resolve();
                return;
            }

            const existing = document.getElementById("leaflet-js");

            if (existing) {
                existing.addEventListener("load", resolve, { once: true });
                return;
            }

            const script = document.createElement("script");
            script.id = "leaflet-js";
            script.src = "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";
            script.onload = resolve;
            script.onerror = reject;

            document.body.appendChild(script);
        });
    }

    return {

        initialize: async function (elementId, lat, lng, ref) {

            await loadLeaflet();

            dotnet = ref;

            map = L.map(elementId).setView([lat, lng], 13);

            L.tileLayer(
                "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                {
                    maxZoom: 19,
                    attribution: "© OpenStreetMap"
                }).addTo(map);

            marker = L.marker([lat, lng], {
                draggable: true
            }).addTo(map);

            map.on("click", function (e) {

                marker.setLatLng(e.latlng);

                dotnet.invokeMethodAsync(
                    "SetCoordinates",
                    e.latlng.lat,
                    e.latlng.lng);
            });

            marker.on("dragend", function () {

                const p = marker.getLatLng();

                dotnet.invokeMethodAsync(
                    "SetCoordinates",
                    p.lat,
                    p.lng);
            });
        },

        search: async function (query) {

            const url =
                `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}`;

            const response = await fetch(url);

            const data = await response.json();

            if (data.length === 0)
                return;

            const lat = parseFloat(data[0].lat);
            const lng = parseFloat(data[0].lon);

            map.setView([lat, lng], 16);

            marker.setLatLng([lat, lng]);

            dotnet.invokeMethodAsync(
                "SetCoordinates",
                lat,
                lng);
        }
    };

})();