let _readAbort = null;
let _lastEventTime = 0;
const DEBOUNCE_MS = 700;

window.nfcHelper = {
    isSupported: () => 'NDEFReader' in window,

    stop: () => {
        if (_readAbort) { _readAbort.abort(); _readAbort = null; }
    },

    write: async (jsonPayload, dotnetRef, spoolmanId) => {
        try {
            const ndef = new NDEFReader();
            const records = [{
                recordType: "mime",
                mediaType: "application/json",
                data: new TextEncoder().encode(jsonPayload)
            }];
            if (spoolmanId != null) {
                records.push({ recordType: "text", data: `SPOOL:${spoolmanId}\nFILAMENT:${spoolmanId}\n` });
            }
            await ndef.write({ records });
            await dotnetRef.invokeMethodAsync('OnWriteSuccess');
        } catch (e) {
            await dotnetRef.invokeMethodAsync('OnWriteError', e.message);
        }
    },

    // Continuous scan — runs until stop() is called.
    // Debounce prevents rapid-fire events from the same tag.
    read: async (dotnetRef) => {
        if (_readAbort) { _readAbort.abort(); _readAbort = null; }
        _lastEventTime = 0;

        _readAbort = new AbortController();
        const signal = _readAbort.signal;

        try {
            const ndef = new NDEFReader();
            await ndef.scan({ signal });

            ndef.onreading = ({ message, serialNumber }) => {
                const now = Date.now();
                if (now - _lastEventTime < DEBOUNCE_MS) return;
                _lastEventTime = now;

                let json = '';
                for (const record of message.records) {
                    if (record.recordType === 'mime' && record.mediaType &&
                        record.mediaType.startsWith('application/json')) {
                        json = new TextDecoder().decode(record.data);
                        break;
                    }
                    if (record.recordType === 'mime' && record.mediaType &&
                        record.mediaType.includes('json')) {
                        json = new TextDecoder().decode(record.data);
                        break;
                    }
                }
                if (!json) {
                    for (const record of message.records) {
                        try {
                            const raw = new TextDecoder().decode(record.data);
                            const idx = raw.indexOf('{');
                            if (idx >= 0) {
                                const candidate = raw.slice(idx);
                                JSON.parse(candidate);
                                json = candidate;
                                break;
                            }
                        } catch { }
                    }
                }

                dotnetRef.invokeMethodAsync('OnTagRead', json, serialNumber || '');
            };

            ndef.onreadingerror = (e) => {
                dotnetRef.invokeMethodAsync('OnReadError', e.message || 'Read error');
            };

            await dotnetRef.invokeMethodAsync('OnScanStarted');
        } catch (e) {
            if (e.name !== 'AbortError') {
                await dotnetRef.invokeMethodAsync('OnReadError', e.message);
            }
        }
    }
};
