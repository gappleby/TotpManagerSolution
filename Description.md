# TOTP Manager — User Guide

## What is TOTP Manager?

TOTP Manager is a private website that keeps all your **two-factor authentication (2FA) codes** in one place. You know those 6-digit numbers that apps like Google Authenticator show you when you log in somewhere? Those are called **TOTP codes** (Time-based One-Time Passwords). They change every 30 seconds, and this site shows them to you live — no phone needed.

Everything stays on your own server. Nothing is sent to the internet.

---

## Getting Your Accounts In

There are four ways to add accounts. Click the **+ Add** button at the top to choose one.

---

### Option 1 — By Text (paste a URL)

This works when an app gives you a text link to copy, or when you already have a line that starts with `otpauth://` or `otpauth-migration://`.

1. Click **+ Add → by Text**
2. Paste the URL into the box
3. Click **Add**

> **Tip for Google Authenticator users:** Open Google Authenticator on your phone, tap the three dots menu, choose **Export accounts**, then scan the QR code it shows using Option 3 (Camera) or Option 2 (Photo) below.

---

### Option 2 — By Photo (upload an image)

If you have a screenshot or photo of a QR code saved on your device:

1. Click **+ Add → Photo**
2. Click **Choose File** and pick the image
3. The site decodes the QR code automatically in your browser — the image is never uploaded to the server
4. If your browser can't decode it, it will try on the server instead

---

### Option 3 — By Camera

Point your device's camera straight at a QR code on screen or on paper:

1. Click **+ Add → Camera**
2. Allow the browser to use your camera when it asks
3. Hold the QR code steady inside the blue square — it scans automatically
4. The camera stops and the account is added as soon as it reads the code

---

### Option 4 — From a Backup ZIP

If you already made a backup of your accounts (see [Backing Up](#backing-up-your-accounts) below):

1. Click **+ Add → ZIP**
2. Choose your `.zip` backup file
3. If the ZIP is password-protected, type the password in the **Archive password** box first
4. Click **Add** — all accounts in the ZIP are merged with any you already have open

---

## Your Account Cards

Once your accounts are added, each one appears as a card on the page.

```
┌─────────────────────────────┐
│         Google              │
│      you@gmail.com          │
│                             │
│    4 8 2  9 1 7   📋        │
│  ████████████░░░░░  18s     │
│                             │
│  ⚙️   ✏️   🗑️               │
└─────────────────────────────┘
```

### The Live Code

The big number in the middle is your current 2FA code. It updates automatically every second. You never need to type it manually — see [Copying the Code](#copying-the-code-to-the-clipboard) below.

### The Countdown Bar

Below the code is a coloured progress bar and a seconds counter:

| Colour | Meaning |
|---|---|
| **Blue** | Plenty of time left |
| **Yellow** | Less than 10 seconds — get ready |
| **Red** | Less than 5 seconds — the code is about to change |

If the bar is nearly empty, just wait a moment and a fresh code will appear.

---

## Copying the Code to the Clipboard

You never need to squint at the screen and type a code manually.

1. Click the **📋 clipboard icon** next to the code on any card
2. The current code is instantly copied
3. Switch to the website or app asking for your code and **paste** it (Ctrl+V on PC, or long-press → Paste on phone)

> The code is valid for the remaining seconds shown on the bar. If you see red, wait for the next code to appear (takes at most 5 seconds) and copy that one instead.

---

## Searching for an Account

If you have lots of accounts, a **search box** appears at the top right of the page.

- Start typing a service name (like `google` or `github`) or an account name
- Cards that don't match are hidden instantly
- Clear the box to show everything again

---

## Editing an Account

Maybe a service name or your username is wrong. You can fix it:

1. Find the card and click the **pencil ✏️ button**
2. An edit form slides open beneath the card
3. Change the **Service** name (shown in bold at the top of the card) and/or the **Account** name (shown in smaller text below)
4. Click **Save**

> Editing only changes the label — the secret code inside stays exactly the same, so your 2FA codes keep working.

---

## Deleting an Account

1. Find the card you want to remove
2. Click the **bin 🗑️ button**
3. Confirm the prompt that appears

The account is removed from the page immediately. If you haven't made a backup yet, it is gone — so back up first if you're not sure!

---

## Viewing the QR Code for an Account

Each account has its own QR code you can scan into a phone app:

1. Click the **gear ⚙️ button** on a card
2. A small QR code appears along with the account type, algorithm, and digit count
3. You can also **Copy URI** to grab the raw `otpauth://` link, or **⬇ PNG** to download the QR image

---

## Backing Up Your Accounts

Always make a backup after adding or changing anything. The backup is a ZIP file containing a QR code image for every account plus a plain text list of all the secret links.

### Without a password

1. Leave the **Archive password** box empty
2. Click **⬇ Backup**
3. Save the file somewhere safe

The downloaded file is named something like `otp-qr-codes-20260222.zip`.

> ⚠️ Without a password anyone who finds this file can see all your secret codes. Only skip the password if you are storing it somewhere already secure.

### With a password

1. Type a password into the **Archive password** box (the lock icon field near the top of the page)
2. Click **⬇ Backup**
3. Save the file — it is now AES-256 encrypted

The downloaded file will be named `otp-qr-codes-20260222-enc.zip` (the `-enc` tells you it is encrypted).

> **Good password tips:** Use at least 8 characters. Mix letters, numbers, and symbols. Don't use your name or birthday. Write it down somewhere safe — if you forget it, the backup cannot be opened.

### How the password is remembered

The password you type is saved in your **browser's local storage**. This means:

- It stays filled in next time you visit the page in the same browser
- It is remembered until you clear it yourself or clear your browser data
- It is only stored in *your* browser — it is never sent to the server by itself

---

## Restoring from a Backup

Restoring **replaces** all currently shown accounts with the ones from the ZIP.

1. If the ZIP is password-protected, type the password into the **Archive password** box first
2. Click **⬇ Restore** (the circular arrow button)
3. If you have accounts open already, a warning will ask you to confirm
4. Pick your `.zip` backup file
5. Your accounts load instantly

---

## Removing a Password from a Backup

Want to make a new, unencrypted backup (or change the password)? Here's how:

1. **Restore** your current encrypted backup (type the password when prompted)
2. **Clear** the **Archive password** box — make sure it is completely empty
3. Click **⬇ Backup**
4. A new ZIP is saved without any password

The old encrypted ZIP is not changed — only the new file you just downloaded is unprotected. Store this new file carefully.

---

## Tips and Good Habits

- **Back up every time you add a new account.** The page holds your accounts in memory — if you close the tab or the server restarts, they are gone unless you backed up.
- **Keep at least two copies of your backup** in different places (e.g. a USB drive and a cloud folder).
- **Test your backup** by restoring it occasionally to make sure it works.
- **Don't share your backup file** with anyone unless it is password-protected and you trust them with the password.
- **If the code doesn't work:** check that your device clock is correct. TOTP codes depend on the time being accurate.
