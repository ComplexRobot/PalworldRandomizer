const DB_NAME = 'palworld-randomizer-recovery';
const STORE_NAME = 'checkpoints';
const DB_VERSION = 1;
const ABORT_MESSAGE = 'The checkpoint save was aborted.';
const OPEN_BLOCKED_MESSAGE = 'The checkpoint database is blocked by another connection.';
const OPEN_FAILED_MESSAGE = 'The checkpoint database could not be opened.';
const READ_FAILED_MESSAGE = 'The checkpoint read transaction failed.';
const WRITE_FAILED_MESSAGE = 'The checkpoint write transaction failed.';
const VERIFY_FAILED_MESSAGE = 'The checkpoint readback did not match the saved JSON.';

let openPromise = null;

function abortError(message) {
    return new DOMException(message || ABORT_MESSAGE, 'AbortError');
}

function openStore() {
    if (openPromise) {
        return openPromise;
    }
    const promise = new Promise((resolve, reject) => {
        let settled = false;
        let request;
        try {
            request = indexedDB.open(DB_NAME, DB_VERSION);
        } catch (error) {
            settled = true;
            reject(error);
            return;
        }
        request.onupgradeneeded = () => {
            const database = request.result;
            if (!database.objectStoreNames.contains(STORE_NAME)) {
                database.createObjectStore(STORE_NAME, {keyPath: 'id'});
            }
        };
        request.onsuccess = () => {
            const database = request.result;
            if (settled) {
                database.close();
                return;
            }
            settled = true;
            database.onversionchange = () => {
                database.close();
                if (openPromise === promise) {
                    openPromise = null;
                }
            };
            resolve(database);
        };
        request.onerror = () => {
            if (settled) {
                return;
            }
            settled = true;
            reject(request.error || new Error(OPEN_FAILED_MESSAGE));
        };
        request.onblocked = () => {
            if (settled) {
                return;
            }
            settled = true;
            reject(new DOMException(OPEN_BLOCKED_MESSAGE, 'InvalidStateError'));
        };
    });
    openPromise = promise;
    promise.catch(() => {
        if (openPromise === promise) {
            openPromise = null;
        }
    });
    return promise;
}

export async function saveCheckpoint(json, signal) {
    if (typeof json !== 'string') {
        throw new TypeError('Checkpoint payload must be a managed JSON string.');
    }
    if (signal && signal.aborted) {
        throw abortError();
    }
    const database = await openStore();
    if (signal && signal.aborted) {
        throw abortError();
    }
    const id = crypto.randomUUID();
    return new Promise((resolve, reject) => {
        let settled = false;
        let transaction;
        let abortedBySignal = false;
        let committed = false;
        const cleanup = () => {
            if (signal) {
                signal.removeEventListener('abort', onAbort);
            }
        };
        const fail = (error) => {
            if (settled) {
                return;
            }
            settled = true;
            cleanup();
            reject(error);
        };
        const onAbort = () => {
            if (committed) {
                return;
            }
            try {
                transaction.abort();
                abortedBySignal = true;
            } catch (error) {
                // Committing/finished transactions cannot abort; wait for their terminal event and readback.
                if (error.name !== 'InvalidStateError') {
                    fail(error);
                }
            }
        };
        try {
            transaction = database.transaction([STORE_NAME], 'readwrite');
        } catch (error) {
            fail(error);
            return;
        }
        transaction.oncomplete = async () => {
            committed = true;
            cleanup();
            try {
                const stored = await readCheckpoint(id);
                if (stored !== json) {
                    fail(new Error(VERIFY_FAILED_MESSAGE));
                    return;
                }
                if (!settled) {
                    settled = true;
                    resolve(id);
                }
            } catch (error) {
                fail(error);
            }
        };
        transaction.onerror = () => {
            fail(abortedBySignal ? abortError() : transaction.error || new Error(WRITE_FAILED_MESSAGE));
        };
        transaction.onabort = () => {
            fail(abortedBySignal ? abortError() : transaction.error || abortError());
        };
        if (signal) {
            signal.addEventListener('abort', onAbort, {once: true});
        }
        try {
            transaction.objectStore(STORE_NAME).add({id: id, json: json});
        } catch (error) {
            fail(error);
            try {
                transaction.abort();
            } catch (abortFailure) {
                // The transaction may already be inactive; report the original error.
            }
        }
    });
}

export async function readCheckpoint(id) {
    const database = await openStore();
    return new Promise((resolve, reject) => {
        let settled = false;
        let value = null;
        let transaction;
        const fail = (error) => {
            if (settled) {
                return;
            }
            settled = true;
            reject(error);
        };
        try {
            transaction = database.transaction([STORE_NAME], 'readonly');
        } catch (error) {
            fail(error);
            return;
        }
        let request;
        try {
            request = transaction.objectStore(STORE_NAME).get(id);
        } catch (error) {
            fail(error);
            try {
                transaction.abort();
            } catch (abortFailure) {
                // The transaction may already be inactive; report the original error.
            }
            return;
        }
        request.onsuccess = () => {
            const record = request.result;
            value = record && typeof record.json === 'string' ? record.json : null;
        };
        transaction.oncomplete = () => {
            if (settled) {
                return;
            }
            settled = true;
            resolve(value);
        };
        transaction.onerror = () => {
            fail(transaction.error || request.error || new Error(READ_FAILED_MESSAGE));
        };
        transaction.onabort = () => {
            fail(transaction.error || new DOMException(READ_FAILED_MESSAGE, 'AbortError'));
        };
    });
}