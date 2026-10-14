window.csmConnectionState = (() => {
    function isOnline() {
        return navigator.onLine;
    }

    function registerConnectionHandlers(dotNetReference) {
        window.addEventListener("online", () => {
            dotNetReference.invokeMethodAsync("OnOnline");
        });

        window.addEventListener("offline", () => {
            dotNetReference.invokeMethodAsync("OnOffline");
        });
    }

    function registerBrowserSyncHandler() {
        window.addEventListener("online", async () => {
            try {
                if (
                    window.csmOfflineSync &&
                    typeof window.csmOfflineSync.syncPending === "function"
                ) {
                    await window.csmOfflineSync.syncPending();
                }
            }
            catch {
                // Leave queued operations for the next synchronization attempt.
            }
        });
    }

    return {
        isOnline,
        registerConnectionHandlers,
        registerBrowserSyncHandler
    };
})();