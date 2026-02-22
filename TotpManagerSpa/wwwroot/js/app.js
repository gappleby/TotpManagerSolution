// app.js — application entry point; wires all event listeners and exposes globals.
// Must be loaded last (after all other TotpSpa modules).

var TotpSpa = window.TotpSpa || {};

TotpSpa.App = (function () {
    'use strict';

    const PASS_KEY = 'totpBackupPassword';

    // ── Error banner ──────────────────────────────────────────────────────

    function showError(msg) {
        const el = document.getElementById('errorAlert');
        if (!el) return;
        el.textContent   = msg || '';
        el.style.display = msg ? '' : 'none';
    }

    function clearError() { showError(''); }

    // ── Password helpers ──────────────────────────────────────────────────

    function _initPassword() {
        const input = document.getElementById('sessionPassword');
        if (!input) return;
        input.value = localStorage.getItem(PASS_KEY) || '';
        input.addEventListener('input', function () {
            if (this.value) localStorage.setItem(PASS_KEY, this.value);
            else            localStorage.removeItem(PASS_KEY);
        });
    }

    function _togglePeek() {
        const sf  = document.getElementById('sessionPassword');
        const btn = document.getElementById('peekBtn');
        const showing = sf.type === 'text';
        sf.type       = showing ? 'password' : 'text';
        btn.innerHTML = showing ? '&#128065;' : '&#128064;';
    }

    // ── Backup ────────────────────────────────────────────────────────────

    async function _handleBackup() {
        const accounts = TotpSpa.Accounts.getAll();
        if (!accounts.length) return;
        const password = document.getElementById('sessionPassword')?.value || '';
        try {
            const blob = await TotpSpa.ZipBackup.createBackupZip(accounts, password || null);
            const date = new Date().toISOString().slice(0, 10).replace(/-/g, '');
            TotpSpa.ZipBackup.triggerDownload(blob, `otp-qr-codes-${date}${password ? '-enc' : ''}.zip`);
        } catch (e) {
            showError('Could not create backup: ' + (e.message || 'Unknown error'));
        }
    }

    // ── Restore ───────────────────────────────────────────────────────────

    function _handleRestoreClick() {
        const count = TotpSpa.Accounts.getAll().length;
        if (count > 0) {
            if (!confirm(`This will replace all ${count} account(s) with the contents of the selected ZIP.\n\nContinue?`))
                return;
        }
        // Copy password into the hidden input so the file-change handler can read it
        // even if the user has dismissed the keyboard on mobile.
        const pwd = document.getElementById('sessionPassword')?.value || '';
        const hidden = document.getElementById('restorePasswordHidden');
        if (hidden) hidden.value = pwd;
        document.getElementById('restoreFileInput').click();
    }

    async function _handleRestoreFile(e) {
        const file = e.target.files?.[0];
        if (!file) return;
        e.target.value = ''; // reset so the same file can be chosen again

        const password = document.getElementById('restorePasswordHidden')?.value || '';
        clearError();
        try {
            const { accounts, wasEncrypted } = await TotpSpa.ZipBackup.restoreFromZip(file, password || null);
            if (accounts.length === 0) { showError('No valid OTP accounts found in the ZIP.'); return; }
            TotpSpa.Accounts.setAll(accounts);

            // Mirror Web app behaviour: clear stored password after restoring an unencrypted backup
            if (!wasEncrypted) {
                localStorage.removeItem(PASS_KEY);
                const passInput = document.getElementById('sessionPassword');
                if (passInput) passInput.value = '';
            }
        } catch (e) {
            showError(e.message || 'Could not restore from ZIP.');
        }
    }

    // ── Startup ───────────────────────────────────────────────────────────

    function _init() {
        _initPassword();

        document.getElementById('peekBtn')         ?.addEventListener('click',  _togglePeek);
        document.getElementById('backupBtn')        ?.addEventListener('click',  _handleBackup);
        document.getElementById('restoreBtn')       ?.addEventListener('click',  _handleRestoreClick);
        document.getElementById('restoreFileInput') ?.addEventListener('change', _handleRestoreFile);
        document.getElementById('addSubmitBtn')     ?.addEventListener('click',  () => TotpSpa.UiAddPanel.handleSubmit());
        document.getElementById('cancelAddBtn')     ?.addEventListener('click',  () => TotpSpa.UiAddPanel.close());
        document.getElementById('stopCameraBtn')    ?.addEventListener('click',  () => TotpSpa.QR.stopCamera());
        document.getElementById('searchInput')      ?.addEventListener('input',  e  => TotpSpa.UiCards.filterCards(e.target.value));

        // Add-mode dropdown items
        document.querySelectorAll('[data-add-mode]').forEach(el =>
            el.addEventListener('click', e => { e.preventDefault(); TotpSpa.UiAddPanel.open(el.dataset.addMode); })
        );

        // Initial render + start TOTP ticker
        TotpSpa.UiCards.render();
        TotpSpa.UiCards.startTick();

        // Stop camera on tab close
        window.addEventListener('beforeunload', () => TotpSpa.QR.stopCamera());
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', _init);
    else
        _init();

    return { showError, clearError };
})();
