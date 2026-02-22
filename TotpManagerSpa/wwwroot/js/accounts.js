// accounts.js — in-memory account array with CRUD and merge.
// Port of the account management logic from Index.cshtml.cs handlers.
// Depends on: otpauth.js, ui-cards.js (called after mutations to re-render)

var TotpSpa = window.TotpSpa || {};

TotpSpa.Accounts = (function () {
    'use strict';

    let _accounts = [];  // Array of AccountRecord objects

    // Deduplication key: "typeLabel|issuer|name" (case-insensitive, trimmed).
    // Port of Index.cshtml.cs AccountKey().
    function accountKey(a) {
        return `${a.typeLabel}|${(a.issuer || '').trim().toLowerCase()}|${(a.name || '').trim().toLowerCase()}`;
    }

    // Port of Index.cshtml.cs MergeAccounts(): incoming replaces existing match, else appends.
    function _merge(existing, incoming) {
        const result = [...existing];
        for (const newAcct of incoming) {
            const key = accountKey(newAcct);
            const idx = result.findIndex(a => accountKey(a) === key);
            if (idx >= 0) result[idx] = newAcct;
            else          result.push(newAcct);
        }
        return result;
    }

    function _rerender() {
        TotpSpa.UiCards.render();
    }

    // Build an AccountRecord from a decoded OtpParameters object (from protobuf).
    // Port of Index.cshtml.cs ProcessMigrationUrlAsync() account construction.
    function buildFromOtpParams(otp) {
        const uri = TotpSpa.OtpAuth.buildUri(otp);
        return TotpSpa.OtpAuth.parseOtpUri(uri);
    }

    function getAll() { return _accounts; }

    // Replace all accounts (Restore operation).
    function setAll(list) {
        _accounts = list;
        _rerender();
    }

    // Merge incoming accounts into existing set (Add operation).
    function addMany(incoming) {
        _accounts = _merge(_accounts, incoming);
        _rerender();
    }

    // Remove one account by URI.
    function deleteOne(uri) {
        _accounts = _accounts.filter(a => a.uri !== uri);
        _rerender();
    }

    // Rename an account's name/issuer and rebuild its URI.
    function editOne(oldUri, newName, newIssuer) {
        const newUri = TotpSpa.OtpAuth.rebuildOtpUri(oldUri, newName, newIssuer);
        _accounts = _accounts.map(a => {
            if (a.uri !== oldUri) return a;
            const parsed = TotpSpa.OtpAuth.parseOtpUri(newUri);
            return parsed || a;
        });
        _rerender();
    }

    return { getAll, setAll, addMany, deleteOne, editOne, buildFromOtpParams, accountKey };
})();
