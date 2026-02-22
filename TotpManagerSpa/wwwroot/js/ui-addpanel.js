// ui-addpanel.js — Add panel lifecycle and all import-mode handlers.
// Mirrors the Add panel behaviour from TotpManager.Web without any server round-trips.
// Depends on: otpauth.js, migration.js, accounts.js, qr.js, zip-backup.js, app.js (showError)

var TotpSpa = window.TotpSpa || {};

TotpSpa.UiAddPanel = (function () {
    'use strict';

    let _mode = 'url';

    // ── Panel open / close ────────────────────────────────────────────────

    function open(mode) {
        _mode = mode || 'url';
        _section('addUrlSection',    mode === 'url');
        _section('addImageSection',  mode === 'image');
        _section('addCameraSection', mode === 'camera');
        _section('addZipSection',    mode === 'zip');

        const submitBtn = document.getElementById('addSubmitBtn');
        if (submitBtn) submitBtn.style.display = mode === 'camera' ? 'none' : '';

        // Reset status indicators when switching mode
        const imgStatus = document.getElementById('imageDecodeStatus');
        if (imgStatus) { imgStatus.style.display = 'none'; imgStatus.textContent = ''; }

        const panel = document.getElementById('addPanel');
        panel.style.display = '';
        panel.scrollIntoView({ behavior: 'smooth', block: 'start' });

        TotpSpa.App.clearError();

        if (mode === 'camera')
            TotpSpa.QR.startCamera(_onCameraResult, _onCameraStatus);
        else
            TotpSpa.QR.stopCamera();
    }

    function close() {
        TotpSpa.QR.stopCamera();
        document.getElementById('addPanel').style.display = 'none';
        TotpSpa.App.clearError();
    }

    function _section(id, visible) {
        const el = document.getElementById(id);
        if (el) el.style.display = visible ? '' : 'none';
    }

    // ── Camera callbacks ──────────────────────────────────────────────────

    function _onCameraResult(url) {
        document.getElementById('addPanel').style.display = 'none';
        _setStatus('QR detected \u2014 adding\u2026', 'success');
        _processInput(url);
    }

    function _onCameraStatus(msg, level) {
        _setStatus(msg, level);
    }

    function _setStatus(msg, level) {
        const el = document.getElementById('scanStatus');
        if (el) { el.textContent = msg; el.className = 'small text-' + (level || 'muted'); }
    }

    // ── Core input processor ──────────────────────────────────────────────

    // Handles both otpauth-migration:// and one-or-more otpauth:// URIs (one per line).
    async function _processInput(text) {
        const lines   = (text || '').split('\n').map(l => l.trim()).filter(Boolean);
        const results = [];

        for (const line of lines) {
            if (line.startsWith('otpauth-migration://')) {
                let payload;
                try {
                    payload = TotpSpa.Migration.parseMigrationUrl(line);
                } catch (e) {
                    TotpSpa.App.showError('Could not parse migration URL: ' + e.message);
                    return;
                }
                for (const otp of payload.otpParameters) {
                    const account = TotpSpa.Accounts.buildFromOtpParams(otp);
                    if (account) results.push(account);
                }
            } else if (line.startsWith('otpauth://')) {
                const account = TotpSpa.OtpAuth.parseOtpUri(line);
                if (account) results.push(account);
            }
        }

        if (results.length === 0) {
            TotpSpa.App.showError('No valid OTP accounts found in the provided input.');
            return;
        }

        TotpSpa.Accounts.addMany(results);
        close();
    }

    // ── Submit handler (called by app.js) ─────────────────────────────────

    async function handleSubmit() {
        TotpSpa.App.clearError();

        if (_mode === 'url') {
            const text = (document.getElementById('addMigrationUrlInput')?.value || '').trim();
            if (!text) { TotpSpa.App.showError('Please paste a migration URL or otpauth:// URI.'); return; }
            await _processInput(text);

        } else if (_mode === 'image') {
            const file = document.getElementById('qrImageInput')?.files?.[0];
            if (!file) { TotpSpa.App.showError('Please select a QR image file.'); return; }

            const statusEl = document.getElementById('imageDecodeStatus');
            const submitBtn = document.getElementById('addSubmitBtn');
            statusEl.style.display = '';
            statusEl.textContent   = 'Decoding QR code\u2026';
            statusEl.className     = 'small text-muted mt-1';
            if (submitBtn) submitBtn.disabled = true;

            const decoded = await TotpSpa.QR.decodeFromFile(file);
            if (submitBtn) submitBtn.disabled = false;

            if (decoded) {
                statusEl.textContent = 'Decoded \u2014 adding\u2026';
                statusEl.className   = 'small text-success mt-1';
                await _processInput(decoded);
            } else {
                statusEl.textContent = 'Could not decode QR code.';
                statusEl.className   = 'small text-danger mt-1';
                TotpSpa.App.showError('Could not decode QR code from this image. Try Camera mode or paste the URL directly.');
            }

        } else if (_mode === 'zip') {
            const file     = document.getElementById('zipAddInput')?.files?.[0];
            if (!file) { TotpSpa.App.showError('Please select a ZIP file.'); return; }
            const password = document.getElementById('sessionPassword')?.value || '';

            try {
                const { accounts } = await TotpSpa.ZipBackup.restoreFromZip(file, password || null);
                if (accounts.length === 0) { TotpSpa.App.showError('No valid OTP accounts found in the ZIP.'); return; }
                TotpSpa.Accounts.addMany(accounts);
                close();
            } catch (e) {
                TotpSpa.App.showError(e.message || 'Could not open ZIP.');
            }
        }
    }

    return { open, close, handleSubmit };
})();
