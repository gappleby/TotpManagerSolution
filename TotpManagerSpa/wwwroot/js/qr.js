// qr.js — QR code decode (image/camera) and QR code generation.
// Decode: BarcodeDetector (Chromium) → jsQR multi-scale — mirrors Web app decode chain.
// Generate: qrcode-generator CDN library (exposes global qrcode function).
// Depends on: jsQR global, qrcode global (qrcode-generator)

var TotpSpa = window.TotpSpa || {};

TotpSpa.QR = (function () {
    'use strict';

    // ── QR Generation ──────────────────────────────────────────────────────

    // Returns a Promise<string> resolving to a PNG data URL.
    // ECCLevel Q matches the server-side QRCoder setting.
    // Uses qrcode-generator (global: qrcode) — auto type number, scales to ~160px.
    function generateDataUrl(uri) {
        return new Promise(function (resolve, reject) {
            try {
                var qr = qrcode(0, 'Q');
                qr.addData(uri, 'Byte');
                qr.make();
                var modules  = qr.getModuleCount();
                var cellSize = Math.max(2, Math.round(160 / modules));
                resolve(qr.createDataURL(cellSize, 2));
            } catch (e) {
                reject(e);
            }
        });
    }

    // ── QR Decoding from File ──────────────────────────────────────────────

    // Tries BarcodeDetector → jsQR at multiple scales.
    // Returns decoded string or null.
    async function decodeFromFile(file) {
        const objectUrl = URL.createObjectURL(file);
        const img = new Image();
        const loaded = await new Promise(res => {
            img.onload  = () => res(true);
            img.onerror = () => res(false);
            img.src = objectUrl;
        });
        URL.revokeObjectURL(objectUrl);
        if (!loaded) return null;

        // 1. Native BarcodeDetector (Chromium ≥83 / Edge)
        if ('BarcodeDetector' in window) {
            try {
                const bd   = new BarcodeDetector({ formats: ['qr_code'] });
                const hits = await bd.detect(img);
                if (hits.length > 0) return hits[0].rawValue;
            } catch { /* not available or blocked */ }
        }

        // 2. jsQR at multiple scales (handles high-density codes better at different sizes)
        const canvas = document.createElement('canvas');
        const ctx    = canvas.getContext('2d');
        for (const scale of [1.0, 0.5, 0.75, 1.5, 2.0]) {
            const w = Math.max(1, Math.round(img.naturalWidth  * scale));
            const h = Math.max(1, Math.round(img.naturalHeight * scale));
            canvas.width = w; canvas.height = h;
            ctx.fillStyle = '#fff';
            ctx.fillRect(0, 0, w, h);
            ctx.drawImage(img, 0, 0, w, h);
            const id   = ctx.getImageData(0, 0, w, h);
            const code = jsQR(id.data, w, h, { inversionAttempts: 'attemptBoth' });
            if (code) return code.data;
        }

        return null;
    }

    // ── Camera ────────────────────────────────────────────────────────────

    let _stream   = null;
    let _interval = null;

    function startCamera(onResult, onStatus) {
        if (!navigator.mediaDevices?.getUserMedia) {
            onStatus('Camera not supported in this browser.', 'danger');
            return;
        }
        navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } } })
            .then(stream => {
                _stream = stream;
                const vid = document.getElementById('cameraVideo');
                if (vid) vid.srcObject = stream;
                onStatus('Point the camera at a migration or individual account QR code\u2026', 'muted');
                _interval = setInterval(() => _scanFrame(onResult), 250);
            })
            .catch(err => onStatus('Camera error: ' + err.message, 'danger'));
    }

    function _scanFrame(onResult) {
        const video  = document.getElementById('cameraVideo');
        const canvas = document.getElementById('cameraCanvas');
        if (!video || !canvas || video.readyState !== video.HAVE_ENOUGH_DATA) return;
        canvas.width = video.videoWidth; canvas.height = video.videoHeight;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0);
        const id   = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const code = jsQR(id.data, id.width, id.height, { inversionAttempts: 'dontInvert' });
        if (code && (code.data.startsWith('otpauth-migration://') || code.data.startsWith('otpauth://'))) {
            stopCamera();
            onResult(code.data);
        }
    }

    function stopCamera() {
        clearInterval(_interval); _interval = null;
        if (_stream) { _stream.getTracks().forEach(t => t.stop()); _stream = null; }
        const vid = document.getElementById('cameraVideo');
        if (vid) vid.srcObject = null;
    }

    return { generateDataUrl, decodeFromFile, startCamera, stopCamera };
})();
