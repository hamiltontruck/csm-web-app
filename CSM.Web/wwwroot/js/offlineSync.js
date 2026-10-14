window.csmOfflineSync = (() => {
    const databaseName = "CSMOfflineSync";
    const databaseVersion = 1;
    const storeName = "operations";

    let apiBaseUrl = "";
    let accessToken = null;

    function openDatabase() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(
                databaseName,
                databaseVersion);

            request.onupgradeneeded = event => {
                const database = event.target.result;

                if (!database.objectStoreNames.contains(storeName)) {
                    const store = database.createObjectStore(
                        storeName,
                        { keyPath: "id" });

                    store.createIndex(
                        "status",
                        "status",
                        { unique: false });

                    store.createIndex(
                        "createdAtUtc",
                        "createdAtUtc",
                        { unique: false });
                }
            };

            request.onsuccess = () => {
                resolve(request.result);
            };

            request.onerror = () => {
                reject(request.error);
            };
        });
    }

    function configureApi(apiUrl, token) {
        apiBaseUrl = apiUrl;
        accessToken = token;
    }

    function getConfigState() {
        return {
            apiBaseUrl,
            hasAccessToken:
                !!accessToken
        };
    }

    async function add(operation) {
        const database = await openDatabase();

        return new Promise((resolve, reject) => {
            const transaction = database.transaction(
                storeName,
                "readwrite");

            transaction.objectStore(storeName).put(operation);

            transaction.oncomplete = () => {
                database.close();
                resolve(true);
            };

            transaction.onerror = () => {
                database.close();
                reject(transaction.error);
            };
        });
    }

    async function getAll() {
        const database = await openDatabase();

        return new Promise((resolve, reject) => {
            const transaction = database.transaction(
                storeName,
                "readonly");

            const request =
                transaction.objectStore(storeName).getAll();

            request.onsuccess = () => {
                database.close();
                resolve(request.result);
            };

            request.onerror = () => {
                database.close();
                reject(request.error);
            };
        });
    }

    async function remove(id) {
        const database = await openDatabase();

        return new Promise((resolve, reject) => {
            const transaction = database.transaction(
                storeName,
                "readwrite");

            transaction.objectStore(storeName).delete(id);

            transaction.oncomplete = () => {
                database.close();
                resolve(true);
            };

            transaction.onerror = () => {
                database.close();
                reject(transaction.error);
            };
        });
    }

    async function syncPending() {
        if (!navigator.onLine) {
            return;
        }

        if (!apiBaseUrl || !accessToken) {
            return;
        }

        const operations = await getAll();

        const pendingOperations = operations
            .filter(x => x.status === "Pending")
            .sort(
                (a, b) =>
                    new Date(a.createdAtUtc) -
                    new Date(b.createdAtUtc));

        for (const operation of pendingOperations) {
            try {
                let url;
                let options;

                if (
                    operation.entityType === "CostCode" &&
                    operation.operationType === "Create"
                ) {
                    url = `${apiBaseUrl}CostCodes`;

                    options = {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            "Authorization":
                                `Bearer ${accessToken}`
                        },
                        body: operation.payloadJson
                    };
                }
                else if (
                    operation.entityType === "CostCode" &&
                    operation.operationType === "Update"
                ) {
                    url =
                        `${apiBaseUrl}CostCodes/${operation.entityId}`;

                    options = {
                        method: "PUT",
                        headers: {
                            "Content-Type": "application/json",
                            "Authorization":
                                `Bearer ${accessToken}`
                        },
                        body: operation.payloadJson
                    };
                }
                else if (
                    operation.entityType === "CostCode" &&
                    operation.operationType === "ChangeStatus"
                ) {
                    url =
                        `${apiBaseUrl}CostCodes/${operation.entityId}/active-status`;

                    options = {
                        method: "PUT",
                        headers: {
                            "Content-Type": "application/json",
                            "Authorization":
                                `Bearer ${accessToken}`
                        },
                        body: operation.payloadJson
                    };
                }
                else if (
                    operation.entityType === "CostCode" &&
                    operation.operationType === "Delete"
                ) {
                    url =
                        `${apiBaseUrl}CostCodes/${operation.entityId}`;

                    options = {
                        method: "DELETE",
                        headers: {
                            "Authorization":
                                `Bearer ${accessToken}`
                        }
                    };
                }
                else {
                    continue;
                }

                const response =
                    await fetch(url, options);

                if (!response.ok) {
                    continue;
                }

                await remove(operation.id);
            }
            catch {
                // Leave the operation queued for the next synchronization attempt.
            }
        }
    }

    async function queueCostCodeCreate(payload) {
        const operation = {
            id: crypto.randomUUID(),
            operationType: "Create",
            entityType: "CostCode",
            entityId: "",
            payloadJson: JSON.stringify(payload),
            createdAtUtc: new Date().toISOString(),
            retryCount: 0,
            status: "Pending",
            lastError: null,
            lastAttemptAtUtc: null
        };

        await add(operation);

        return operation.id;
    }

    return {
        add,
        queue: add,
        getAll,
        remove,
        configureApi,
        syncPending,
        getConfigState,
        queueCostCodeCreate
    };
})();