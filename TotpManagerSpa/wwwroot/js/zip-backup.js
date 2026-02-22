// zip-backup.js — ZIP backup creation and restore using @zip.js/zip.js.
// AES-256 encryption is WinZip-compatible (same as SharpZipLib used by TotpManager.Web),
// so backups are interchangeable between the SPA and the Web app.
// Depends on: zip global (zip-full.min.js), otpauth.js, qr.js

var TotpSpa = window.TotpSpa || {};

// Disable web workers — codecs run on main thread via the bundled zip-full build.
zip.configure({ useWebWorkers: false });

TotpSpa.ZipBackup = (function () {
    'use strict';

    function sanitizeFilename(name) {
        return (name || 'account')
            .replace(/[<>:"/\\|?*\x00-\x1f]/g, '_')
            .replace(/\.+$/, '')
            .trim() || 'account';
    }

    function dataUrlToBytes(dataUrl) {
        const base64 = dataUrl.split(',')[1];
        const binary = atob(base64);
        const bytes  = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        return bytes;
    }

    // Creates a backup ZIP matching the TotpManager.Web format:
    //   accounts.txt  — newline-delimited otpauth:// URIs (primary restore path)
    //   <label>.png   — individual QR code images
    // If password is truthy, all entries are AES-256 encrypted (WinZip format).
    async function createBackupZip(accounts, password) {
        const { ZipWriter, BlobWriter, Uint8ArrayReader, TextReader } = zip;
        const hasPassword = !!(password && password.length);
        const writerOptions = hasPassword
            ? { password, encryptionStrength: 3 }  // 3 = AES-256
            : {};

        const blobWriter = new BlobWriter('application/zip');
        const zipWriter  = new ZipWriter(blobWriter, writerOptions);

        // Sort: issuer ASC, name ASC (case-insensitive) — mirrors C# LINQ order
        const sorted = [...accounts].sort((a, b) => {
            const ic = (a.issuer || '').localeCompare(b.issuer || '', undefined, { sensitivity: 'base' });
            return ic !== 0 ? ic : (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' });
        });

        const seen    = {};
        const uriList = [];

        for (const account of sorted) {
            uriList.push(account.uri);

            const label    = account.issuer ? `${account.issuer} - ${account.name}` : account.name;
            const baseName = sanitizeFilename(label);
            seen[baseName] = (seen[baseName] || 0) + 1;
            const entryName = seen[baseName] === 1
                ? `${baseName}.png`
                : `${baseName} (${seen[baseName] - 1}).png`;

            const dataUrl  = await TotpSpa.QR.generateDataUrl(account.uri);
            const pngBytes = dataUrlToBytes(dataUrl);
            await zipWriter.add(entryName, new Uint8ArrayReader(pngBytes));
        }

        // accounts.txt — always included; allows restore on Linux where image decode is unavailable
        await zipWriter.add('accounts.txt', new TextReader(uriList.join('\n')));
        await zipWriter.close();
        return blobWriter.getData();
    }

    // Restores from a ZIP file.
    // Reads accounts.txt first (preferred, cross-platform).
    // Returns { accounts: AccountRecord[], wasEncrypted: boolean }
    async function restoreFromZip(file, password) {
        const { ZipReader, BlobReader, TextWriter } = zip;
        const hasPassword = !!(password && password.length);
        const readerOpts  = hasPassword ? { password } : {};
        const zipReader   = new ZipReader(new BlobReader(file), readerOpts);

        let entries;
        try {
            entries = await zipReader.getEntries();
        } catch (e) {
            await zipReader.close().catch(() => {});
            throw new Error('Could not open ZIP file. ' + (hasPassword ? 'Check the password.' : 'File may be corrupt.'));
        }

        let accountsText = null;
        let wasEncrypted = false;

        for (const entry of entries) {
            if (entry.encrypted) wasEncrypted = true;
            if (entry.filename.toLowerCase() === 'accounts.txt') {
                try {
                    accountsText = await entry.getData(new TextWriter());
                } catch {
                    await zipReader.close().catch(() => {});
                    throw new Error('Wrong password or corrupted ZIP.');
                }
                break;
            }
        }

        await zipReader.close().catch(() => {});

        const accounts = [];
        if (accountsText) {
            const uris = accountsText.split('\n')
                .map(l => l.trim())
                .filter(l => l.startsWith('otpauth://'));
            for (const uri of uris) {
                const parsed = TotpSpa.OtpAuth.parseOtpUri(uri);
                if (parsed) accounts.push(parsed);
            }
        }
        // Note: legacy image-only ZIPs (no accounts.txt) cannot be decoded client-side
        // because BarcodeDetector/jsQR require a rendered DOM image element, not a zip stream.
        // All ZIPs created by TotpManager.Web or this SPA include accounts.txt.

        return { accounts, wasEncrypted };
    }

    // Triggers a browser file download for a Blob.
    function triggerDownload(blob, filename) {
        const url = URL.createObjectURL(blob);
        const a   = document.createElement('a');
        a.href = url; a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(() => URL.revokeObjectURL(url), 10000);
    }

    return { createBackupZip, restoreFromZip, triggerDownload };
})();
