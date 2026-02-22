// protobuf.js — hand-rolled proto3 wire-format decoder.
// Direct JavaScript port of TotpManager.Core/ProtobufDecoder.cs
//
// MigrationPayload field numbers:  1=otp_parameters(msg) 2=version 3=batch_size 4=batch_index 5=batch_id
// OtpParameters field numbers:     1=secret(bytes) 2=name 3=issuer 4=algorithm 5=digits 6=type 7=counter 8=id
//
// Algorithm enum: 0=Unspecified 1=SHA1 2=SHA256 3=SHA512 4=MD5
// DigitCount enum: 0=Unspecified 1=Six 2=Eight
// OtpType enum:   0=Unspecified 1=HOTP 2=TOTP

var TotpSpa = window.TotpSpa || {};

TotpSpa.Protobuf = (function () {
    'use strict';

    function makeReader(bytes) {
        const data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        let pos = 0;

        function readByte() {
            if (pos >= data.length) throw new Error('Unexpected end of protobuf data');
            return data[pos++];
        }

        // Read a varint. Uses two 32-bit halves to handle up to 64 bits without BigInt.
        function readVarint() {
            let lo = 0, hi = 0, b;
            // low 28 bits
            for (let shift = 0; shift < 28; shift += 7) {
                b = readByte();
                lo |= (b & 0x7f) << shift;
                if (!(b & 0x80)) return lo >>> 0;
            }
            // bits 28-31 land in lo, bits 32+ land in hi
            b = readByte();
            lo |= (b & 0x0f) << 28;
            hi = (b & 0x7f) >> 4;
            if (!(b & 0x80)) return (lo >>> 0) + hi * 4294967296;
            for (let shift = 3; shift <= 31; shift += 7) {
                b = readByte();
                hi |= (b & 0x7f) << shift;
                if (!(b & 0x80)) break;
            }
            return (lo >>> 0) + hi * 4294967296;
        }

        function readTag() {
            const tag = readVarint();
            return { field: (tag >>> 3) | 0, wire: tag & 7 };
        }

        function readBytes() {
            const len = readVarint() | 0;
            const slice = data.slice(pos, pos + len);
            pos += len;
            return slice;
        }

        function readString() {
            return new TextDecoder('utf-8').decode(readBytes());
        }

        function skipField(wire) {
            switch (wire) {
                case 0: readVarint(); break;
                case 1: pos += 8; break;
                case 2: readBytes(); break;
                case 5: pos += 4; break;
                default: throw new Error('Unknown wire type: ' + wire);
            }
        }

        return {
            get hasMore() { return pos < data.length; },
            readVarint, readTag, readBytes, readString, skipField
        };
    }

    function decodeOtpParameters(bytes) {
        const r = makeReader(bytes);
        const otp = {
            secret: new Uint8Array(0), name: '', issuer: '',
            algorithm: 0, digits: 0, type: 0, counter: 0, id: 0
        };
        while (r.hasMore) {
            const { field, wire } = r.readTag();
            switch (field) {
                case 1: otp.secret    = r.readBytes();   break; // bytes
                case 2: otp.name      = r.readString();  break;
                case 3: otp.issuer    = r.readString();  break;
                case 4: otp.algorithm = r.readVarint();  break; // Algorithm enum
                case 5: otp.digits    = r.readVarint();  break; // DigitCount enum
                case 6: otp.type      = r.readVarint();  break; // OtpType enum
                case 7: otp.counter   = r.readVarint();  break;
                case 8: otp.id        = r.readVarint();  break;
                default: r.skipField(wire);
            }
        }
        return otp;
    }

    function decodePayload(bytes) {
        const r = makeReader(bytes);
        const payload = { otpParameters: [], version: 0, batchSize: 0, batchIndex: 0, batchId: 0 };
        while (r.hasMore) {
            const { field, wire } = r.readTag();
            switch (field) {
                case 1: payload.otpParameters.push(decodeOtpParameters(r.readBytes())); break;
                case 2: payload.version    = r.readVarint(); break;
                case 3: payload.batchSize  = r.readVarint(); break;
                case 4: payload.batchIndex = r.readVarint(); break;
                case 5: payload.batchId    = r.readVarint(); break;
                default: r.skipField(wire);
            }
        }
        return payload;
    }

    return { decodePayload, decodeOtpParameters };
})();
