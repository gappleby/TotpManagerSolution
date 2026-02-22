// migration.js — parses Google Authenticator migration URLs.
// Port of TotpManager.Core/MigrationParser.cs
// Depends on: protobuf.js

var TotpSpa = window.TotpSpa || {};

TotpSpa.Migration = (function () {
    'use strict';

    // Port of MigrationParser.Parse()
    // Accepts: otpauth-migration://offline?data=BASE64_PROTOBUF
    function parseMigrationUrl(url) {
        const u = new URL(url);
        let data = u.searchParams.get('data');
        if (!data) throw new Error("Migration URL has no 'data' query parameter.");

        // URLSearchParams decodes '+' as space (application/x-www-form-urlencoded rules).
        // The base64 payload uses '+' as a valid character, so restore it — same as
        // the C# MigrationParser which calls data.Replace(' ', '+').
        data = data.replace(/ /g, '+');

        // Add base64 padding if stripped during URL encoding
        const padded = data + '='.repeat((4 - (data.length % 4)) % 4);

        const binary = atob(padded);
        const bytes  = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

        return TotpSpa.Protobuf.decodePayload(bytes);
    }

    return { parseMigrationUrl };
})();
