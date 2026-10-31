const key = "garantie-express:issuance-request";

function parseRequest(value) {
    try {
        const request = JSON.parse(value);
        if (typeof request?.id === "string"
            && /^[0-9a-f]{32}$/i.test(request.id)
            && typeof request.requestedAt === "string"
            && Number.isFinite(Date.parse(request.requestedAt))) {
            return request;
        }
    } catch { /* Invalid recovery metadata is repaired by the storage operation. */ }

    return null;
}

function readAndRepairStoredRequest() {
    const value = localStorage.getItem(key);
    if (!value) return null;

    const request = parseRequest(value);
    if (request) return request;

    localStorage.removeItem(key);
    return null;
}

export function get() {
    return navigator.locks.request(key, () => readAndRepairStoredRequest());
}

export function save(request) {
    return navigator.locks.request(key, () => {
        const current = readAndRepairStoredRequest();
        if (current) {
            return current.id === request.id
                && current.requestedAt === request.requestedAt;
        }

        localStorage.setItem(key, JSON.stringify(request));
        return true;
    });
}

export function clear(requestId) {
    return navigator.locks.request(key, () => {
        if (readAndRepairStoredRequest()?.id === requestId) localStorage.removeItem(key);
    });
}
