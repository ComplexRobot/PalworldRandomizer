// App-owned one-shot manual software-recovery control, marker and full-document reload.
import {readCheckpoint, saveCheckpoint} from './recovery-store.js';

const MARKER_KEY = 'palworld-randomizer.recovery.manual';
const MARKER_SCHEMA = 1;
const MANUAL_REASON = 'ManualRecovery';
const FALLBACK = {
    "recovery.manualAction": "Restart in software rendering",
    "recovery.manualHint":
        "Saves this tab's current work, then reloads this page with software " +
        "rendering. Your saved rendering preference is not changed.",
    "recovery.preparing": "Preparing a recovery checkpoint…",
    "recovery.restoring": "Restoring your work into a fresh software-rendered view…",
    "recovery.active":
        "Software rendering is active. Your work was restored; select game files " +
        "again before generating.",
    "recovery.notReady": "Recovery is not ready yet. Wait for the application to finish starting.",
    "recovery.refusedBusy":
        "Recovery paused: setup validation is still running. Your work is " +
        "unchanged.",
    "recovery.refusedPicker": "Recovery paused: a file picker is open. Finish or cancel it first.",
    "recovery.refusedDuplicate": "Recovery is already being prepared in this tab.",
    "recovery.refusedAlreadySoftware":
        "Software rendering is already active in this tab for this session. Your " +
        "saved preference was not changed.",
    "recovery.captureFailed":
        "The current work could not be captured. Nothing was reloaded and your " +
        "work is unchanged.",
    "recovery.storeFailed":
        "The recovery checkpoint could not be saved. Nothing was reloaded and " +
        "your work is unchanged.",
    "recovery.markerFailed":
        "The recovery launch marker could not be verified. Nothing was reloaded " +
        "and your work is unchanged.",
    "recovery.ackFailed":
        "The recovery marker could not be acknowledged. Input remains disabled to" +
        " prevent restoring stale work.",
    "recovery.pendingMissing":
        "The pending recovery checkpoint is missing. Nothing was restored and the" +
        " existing records were kept.",
    "recovery.pendingInvalid":
        "The pending recovery checkpoint is invalid or unsupported. Nothing was " +
        "restored and the existing records were kept.",
    "recovery.markerInvalid":
        "The recovery marker has an unsupported format. Startup was stopped and " +
        "the existing data was kept.",
    "recovery.budgetSpent":
        "The one recovery launch for this tab was already used. Startup was " +
        "stopped and the existing data was kept.",
};

let strings = {...FALLBACK};
let bootInfo = null;
let requestInFlight = false;

function statusElement() {
    return document.getElementById('recovery-status');
}

function setStatus(key, detail) {
    const element = statusElement();
    if (!element) {
        return;
    }
    const text = strings[key] || FALLBACK[key] || key;
    element.textContent = detail ? text + ' (' + detail + ')' : text;
}

function setBusy(busy) {
    const button = document.getElementById('manual-software-recovery');
    if (button) {
        button.disabled = busy;
        button.setAttribute('aria-busy', busy ? 'true' : 'false');
    }
}

function errorDetail(error) {
    if (error && typeof error.message === 'string' && error.message.length > 0) {
        return error.message;
    }
    return String(error || 'Unknown failure');
}

async function loadStrings() {
    try {
        const response = await fetch('./Recovery.en-US.json', {cache: 'no-store'});
        if (response.ok) {
            const loaded = await response.json();
            strings = {...FALLBACK, ...loaded};
            applyStaticLabels();
        }
    } catch (error) {
        // The honest English bootstrap labels remain visible for this startup only.
    }
}

function applyStaticLabels() {
    const button = document.getElementById('manual-software-recovery');
    const hint = document.getElementById('recovery-hint');
    if (button && strings['recovery.manualAction']) {
        button.textContent = strings['recovery.manualAction'];
    }
    if (hint && strings['recovery.manualHint']) {
        hint.textContent = strings['recovery.manualHint'];
    }
}

function findFunction(name) {
    return typeof globalThis[name] === 'function' ? globalThis[name] : null;
}

async function waitFunction(name, timeoutMs) {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
        const found = findFunction(name);
        if (found) {
            return found;
        }
        await new Promise(resolve => setTimeout(resolve, 25));
    }
    return null;
}

function dispatchBoot(info) {
    const detail = {
        status: info.status,
        mode: info.mode || 'normal',
        messageKey: info.messageKey || null,
        detail: info.detail || null,
    };
    document.dispatchEvent(new CustomEvent('recoveryboot', {detail}));
}

function validMarker(marker) {
    if (!marker || typeof marker !== 'object' || Array.isArray(marker)
        || marker.schema !== MARKER_SCHEMA || marker.forceSoftware !== true) {
        return false;
    }
    if (marker.reason !== MANUAL_REASON) {
        return false;
    }
    if (typeof marker.locale !== 'string' || marker.locale.length === 0) {
        return false;
    }
    if (!Number.isInteger(marker.attemptsRemaining) || marker.attemptsRemaining < 0
        || marker.attemptsRemaining > 1) {
        return false;
    }
    if (typeof marker.acknowledged !== 'boolean') {
        return false;
    }
    if (marker.acknowledged) {
        return marker.pending === null && marker.attemptsRemaining === 0;
    }
    const pending = marker.pending;
    if (!pending || typeof pending !== 'object' || Array.isArray(pending)) {
        return false;
    }
    if (typeof pending.id !== 'string' || pending.id.length === 0) {
        return false;
    }
    return pending.reason === marker.reason && pending.locale === marker.locale;
}

function readMarkerRaw() {
    return sessionStorage.getItem(MARKER_KEY);
}

function readMarker() {
    const raw = readMarkerRaw();
    if (!raw) {
        return null;
    }
    const marker = JSON.parse(raw);
    if (!validMarker(marker)) {
        throw new Error('unsupported recovery marker');
    }
    return marker;
}

function writeMarker(marker) {
    const text = JSON.stringify(marker);
    sessionStorage.setItem(MARKER_KEY, text);
    return sessionStorage.getItem(MARKER_KEY) === text;
}

function refuse(key, detail) {
    return {status: 'refused', messageKey: key, detail: detail || ''};
}

async function prepareBoot() {
    let raw;
    let marker;
    try {
        raw = readMarkerRaw();
        marker = readMarker();
    } catch (error) {
        // Schema and reason refusals keep the marker byte-for-byte for diagnosis.
        return refuse('recovery.markerInvalid', errorDetail(error));
    }
    if (!marker) {
        return {status: 'none', mode: 'normal', forced: false};
    }
    if (marker.pending === null) {
        return {status: 'none', mode: 'software', forced: true};
    }
    if (marker.attemptsRemaining < 1) {
        return refuse('recovery.budgetSpent');
    }
    let json = null;
    try {
        json = await readCheckpoint(marker.pending.id);
    } catch (error) {
        return refuse('recovery.pendingMissing', errorDetail(error));
    }
    if (typeof json !== 'string' || json.trim().length === 0) {
        return refuse('recovery.pendingMissing');
    }
    try {
        const state = JSON.parse(json);
        const capturedLocale = state && state.Preferences ? state.Preferences.Locale : null;
        if (capturedLocale !== marker.locale) {
            return refuse('recovery.markerInvalid', 'captured locale does not match the marker');
        }
    } catch (error) {
        // The managed deserializer owns full schema validation; a corrupt record is refused there.
    }
    const spent = {...marker, attemptsRemaining: 0};
    if (!writeMarker(spent)) {
        try {
            sessionStorage.setItem(MARKER_KEY, raw);
        } catch (restoreError) {
            // The pending identity is still present in the last atomic write.
        }
        return refuse('recovery.markerFailed', 'the launch budget could not be recorded');
    }
    return {
        status: 'pending',
        mode: 'software',
        forced: true,
        reason: marker.reason,
        locale: marker.locale,
        pending: {id: marker.pending.id, reason: marker.pending.reason, locale: marker.pending.locale, json},
    };
}

function armMarker(id, reason, locale) {
    const marker = {
        schema: MARKER_SCHEMA,
        forceSoftware: true,
        attemptsRemaining: 1,
        reason,
        locale,
        pending: {id, reason, locale},
        acknowledged: false,
    };
    if (!writeMarker(marker)) {
        throw new Error('recovery marker readback did not match');
    }
}

async function acknowledgePending(info) {
    const marker = readMarker();
    if (!marker || !marker.pending || marker.pending.id !== info.pending.id) {
        return {ok: false, detail: 'pending marker identity changed'};
    }
    const next = {
        ...marker,
        pending: null,
        acknowledged: true,
        attemptsRemaining: 0,
    };
    if (!writeMarker(next)) {
        return {ok: false, detail: 'acknowledgment readback did not match'};
    }
    return {ok: true, detail: ''};
}

async function completePending(info) {
    setStatus('recovery.restoring');
    const wait = await waitFunction('manualRecoveryWaitRestore', 30000);
    if (!wait) {
        setStatus('recovery.ackFailed', 'restore host unavailable');
        return;
    }
    let restored;
    try {
        restored = JSON.parse(await wait());
    } catch (error) {
        setStatus('recovery.ackFailed', errorDetail(error));
        return;
    }
    if (restored.status !== 'restored') {
        setStatus(restored.messageKey || 'recovery.ackFailed', restored.detail || '');
        return;
    }
    let acknowledgment;
    try {
        acknowledgment = await acknowledgePending(info);
    } catch (error) {
        acknowledgment = {ok: false, detail: errorDetail(error)};
    }
    const acknowledge = await waitFunction('manualRecoveryAcknowledge', 30000);
    if (!acknowledge) {
        setStatus('recovery.ackFailed', 'acknowledgment host unavailable');
        return;
    }
    let result;
    try {
        result = JSON.parse(await acknowledge(JSON.stringify(acknowledgment)));
    } catch (error) {
        setStatus('recovery.ackFailed', errorDetail(error));
        return;
    }
    setStatus(result.messageKey || 'recovery.ackFailed', result.detail || '');
    if (result.status === 'active') {
        setBusy(false);
    }
}

async function requestRecovery() {
    if (requestInFlight) {
        return;
    }
    const capture = findFunction('manualRecoveryCapture');
    if (!capture) {
        setStatus('recovery.notReady');
        return;
    }
    requestInFlight = true;
    setBusy(true);
    setStatus('recovery.preparing');
    let reloading = false;
    try {
        const captured = JSON.parse(await capture());
        if (captured.status !== 'captured') {
            setStatus(captured.messageKey || 'recovery.captureFailed', captured.detail || '');
            return;
        }
        let id;
        try {
            id = await saveCheckpoint(captured.json);
        } catch (error) {
            const failure = findFunction('manualRecoveryFailure');
            const rolled = failure ? JSON.parse(await failure(errorDetail(error))) : null;
            setStatus('recovery.storeFailed', rolled && rolled.detail ? rolled.detail : errorDetail(error));
            return;
        }
        try {
            armMarker(id, captured.reason || MANUAL_REASON, captured.locale || 'en-US');
        } catch (error) {
            const failure = findFunction('manualRecoveryMarkerFailure');
            if (failure) {
                await failure(errorDetail(error));
            }
            setStatus('recovery.markerFailed', errorDetail(error));
            return;
        }
        reloading = true;
        location.reload();
    } catch (error) {
        setStatus('recovery.captureFailed', errorDetail(error));
    } finally {
        if (!reloading) {
            requestInFlight = false;
            setBusy(false);
        }
    }
}

async function announceBoot() {
    bootInfo = await prepareBoot();
    if (bootInfo.status === 'refused') {
        setStatus(bootInfo.messageKey, bootInfo.detail || '');
        dispatchBoot(bootInfo);
        return;
    }
    const announce = await waitFunction('recoveryBoot', 30000);
    if (!announce) {
        const key = bootInfo.status === 'pending' ? 'recovery.pendingMissing' : 'recovery.notReady';
        setStatus(key, 'the managed boot host is unavailable');
        dispatchBoot({...bootInfo, status: 'refused', messageKey: key, detail: 'the managed boot host is unavailable'});
        return;
    }
    let decision;
    try {
        decision = JSON.parse(await announce(JSON.stringify(bootInfo)));
    } catch (error) {
        setStatus('recovery.markerInvalid', errorDetail(error));
        dispatchBoot({...bootInfo, status: 'refused', messageKey: 'recovery.markerInvalid', detail: errorDetail(error)});
        return;
    }
    if (!decision || decision.status !== 'accepted') {
        const key = decision && decision.messageKey ? decision.messageKey : 'recovery.pendingInvalid';
        setStatus(key, decision && decision.detail ? decision.detail : '');
        dispatchBoot({...bootInfo, status: 'refused', messageKey: key, detail: decision ? decision.detail : null});
        return;
    }
    dispatchBoot(bootInfo);
    if (bootInfo.status === 'pending') {
        await completePending(bootInfo);
    }
}

function installControls() {
    document.getElementById('manual-software-recovery')?.addEventListener('click', requestRecovery);
}

installControls();
void loadStrings();
void announceBoot();