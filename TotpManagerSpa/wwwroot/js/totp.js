// totp.js — client-side RFC 6238 TOTP generation.
// Extracted and modularised from TotpManager.Web/Pages/Index.cshtml inline script.
// Depends on: otpauth.js (base32Decode)

var TotpSpa = window.TotpSpa || {};

TotpSpa.Totp = (function () {
    'use strict';

    // RFC 6238 TOTP via RFC 2104 HMAC.
    // Uses Web Crypto API in secure contexts (HTTPS/localhost), falls back to jsSHA.
    async function generate({ secret, digits, algorithm, period }) {
        const counter = Math.floor(Date.now() / 1000 / period);
        const buf = new ArrayBuffer(8);
        new DataView(buf).setUint32(4, counter, false); // big-endian counter; high word stays 0

        const keyBytes = TotpSpa.OtpAuth.base32Decode(secret);
        let hmac;

        if (window.crypto?.subtle) {
            // Secure context: native Web Crypto API
            const algMap = { SHA1: 'SHA-1', SHA256: 'SHA-256', SHA512: 'SHA-512' };
            const cryptoAlg = algMap[algorithm] ?? 'SHA-1';
            const key = await crypto.subtle.importKey(
                'raw', keyBytes,
                { name: 'HMAC', hash: cryptoAlg },
                false, ['sign']
            );
            hmac = new Uint8Array(await crypto.subtle.sign('HMAC', key, buf));
        } else {
            // Non-secure context (plain HTTP): jsSHA fallback
            const jshaAlg = algorithm === 'SHA256' ? 'SHA-256'
                          : algorithm === 'SHA512' ? 'SHA-512' : 'SHA-1';
            const shaObj = new jsSHA(jshaAlg, 'ARRAYBUFFER', {
                hmacKey: { value: keyBytes.buffer, format: 'ARRAYBUFFER' }
            });
            shaObj.update(buf);
            hmac = new Uint8Array(shaObj.getHash('ARRAYBUFFER'));
        }

        // Dynamic truncation (RFC 4226 §5.4)
        const offset = hmac[hmac.length - 1] & 0x0f;
        const code   = ((hmac[offset]     & 0x7f) << 24)
                     | ((hmac[offset + 1] & 0xff) << 16)
                     | ((hmac[offset + 2] & 0xff) << 8)
                     |  (hmac[offset + 3] & 0xff);

        return String(code % (10 ** digits)).padStart(digits, '0');
    }

    // Split code in the middle with a thin-space for readability ("482 917")
    function groupCode(code) {
        const mid = Math.floor(code.length / 2);
        return code.slice(0, mid) + '\u2009' + code.slice(mid);
    }

    return { generate, groupCode };
})();
