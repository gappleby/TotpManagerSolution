// otpauth.js — base32 encode/decode, otpauth:// URI builder/parser/rebuilder.
// Direct JavaScript port of TotpManager.Core/OtpAuthBuilder.cs
// plus the URI parsing helpers from Index.cshtml.cs.

var TotpSpa = window.TotpSpa || {};

TotpSpa.OtpAuth = (function () {
    'use strict';

    const BASE32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

    // Port of OtpAuthBuilder.Base32Encode (RFC 4648, no padding)
    function base32Encode(bytes) {
        if (!bytes || !bytes.length) return '';
        let buffer = 0, bitsLeft = 0, result = '';
        for (const b of bytes) {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5) {
                bitsLeft -= 5;
                result += BASE32[(buffer >> bitsLeft) & 0x1f];
            }
        }
        if (bitsLeft > 0) result += BASE32[(buffer << (5 - bitsLeft)) & 0x1f];
        return result;
    }

    // Used by totp.js for generating live codes
    function base32Decode(str) {
        str = str.toUpperCase().replace(/[^A-Z2-7]/g, '');
        const out = [];
        let bits = 0, val = 0;
        for (const c of str) {
            val = (val << 5) | BASE32.indexOf(c);
            bits += 5;
            if (bits >= 8) { out.push((val >>> (bits - 8)) & 0xff); bits -= 8; }
        }
        return new Uint8Array(out);
    }

    // Algorithm enum int → label string  (C# AlgorithmName())
    const ALG_NAMES = { 0: 'SHA1', 1: 'SHA1', 2: 'SHA256', 3: 'SHA512', 4: 'MD5' };

    // Port of OtpAuthBuilder.BuildUri()
    function buildUri(otp) {
        const host   = otp.type === 1 ? 'hotp' : 'totp';   // OtpType.HOTP = 1
        const path   = '/' + encodeURIComponent(otp.name);
        const secret = base32Encode(otp.secret);

        let uri = `otpauth://${host}${path}?secret=${secret}`;
        if (otp.issuer) uri += '&issuer=' + encodeURIComponent(otp.issuer);

        // Omit algorithm when SHA1/Unspecified (default)
        if (otp.algorithm !== 0 && otp.algorithm !== 1)
            uri += '&algorithm=' + (ALG_NAMES[otp.algorithm] || 'SHA1');

        // Omit digits when Six/Unspecified (default = 6)
        if (otp.digits === 2) uri += '&digits=8';

        if (otp.type === 1) uri += '&counter=' + otp.counter;  // HOTP
        else                uri += '&period=30';                // TOTP

        return uri;
    }

    // Parses an otpauth:// URI into an account object.
    // Returns null on failure.
    function parseOtpUri(uri) {
        try {
            const u = new URL(uri);
            const typeLabel = u.host === 'hotp' ? 'HOTP' : 'TOTP';
            const label = decodeURIComponent(u.pathname.replace(/^\//, ''));
            const colon = label.indexOf(':');
            let name   = colon >= 0 ? label.slice(colon + 1) : label;
            let issuer = colon >= 0 ? label.slice(0, colon)  : '';
            const p = new URLSearchParams(u.search);
            if (p.get('issuer')) issuer = p.get('issuer');
            const algLabel = (p.get('algorithm') || 'SHA1').toUpperCase();
            const digits   = parseInt(p.get('digits')  || '6',  10);
            const period   = parseInt(p.get('period')  || '30', 10);
            const secret   = p.get('secret') || '';
            return { uri, name, issuer, typeLabel, algLabel, digits, period, secret };
        } catch { return null; }
    }

    // Port of Index.cshtml.cs RebuildOtpUri()
    function rebuildOtpUri(oldUri, newName, newIssuer) {
        try {
            const u = new URL(oldUri);
            const p = new URLSearchParams(u.search);
            const label = newIssuer
                ? encodeURIComponent(newIssuer) + ':' + encodeURIComponent(newName)
                : encodeURIComponent(newName);
            if (newIssuer) p.set('issuer', newIssuer);
            else           p.delete('issuer');
            return `otpauth://${u.host}/${label}?${p.toString().replace(/\+/g, '%20')}`;
        } catch { return oldUri; }
    }

    // Port of Index.cshtml.cs LabelFromOtpUri()
    function labelFromOtpUri(uri) {
        try {
            const parsed = parseOtpUri(uri);
            if (!parsed) return 'account';
            return parsed.issuer ? `${parsed.issuer} - ${parsed.name}` : parsed.name;
        } catch { return 'account'; }
    }

    return { base32Encode, base32Decode, buildUri, parseOtpUri, rebuildOtpUri, labelFromOtpUri };
})();
