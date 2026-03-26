# PWA Features — It's a Real App

> **Slide talking points:** Most people don't realize a website can do all of this. Good "wow factor" slide.

---

## What Is a PWA?

- **Progressive Web App** — a website that behaves like a native app
- Installable from the browser, no App Store required
- Works on iOS, Android, Windows, Mac
- Can receive shared content from other apps (like a native app can)

---

## Feature 1: Installable App

- Browser detects the `manifest.json` → shows "Install" button
- User taps → added to home screen with its own icon
- Launches in **standalone mode** (no browser UI — looks like a native app)
- Purple theme (`#a855f7`) applied to the status bar on mobile

```json
{
  "display": "standalone",
  "theme_color": "#a855f7",
  "background_color": "#a855f7"
}
```

**No App Store. No review process. No version management.** Updates happen automatically on next visit.

---

## Feature 2: Web Share Target API

This is the real magic.

```json
"share_target": {
  "action": "/",
  "method": "GET",
  "params": {
    "url": "url",
    "text": "text",
    "title": "title"
  }
}
```

- MusicShare appears in the **iOS/Android share sheet** — alongside Messages, Twitter, etc.
- User opens Spotify → finds a song → taps Share → selects MusicShare
- The Spotify URL is sent to MusicShare automatically
- The form pre-fills and triggers resolution immediately

**This is only possible because it's installed as a PWA.** Regular websites can't appear in the share sheet.

---

## Feature 3: Incremental Static Regeneration (ISR)

Not PWA-specific, but worth covering here — it's what makes share links fast.

```
First time /share/abc123 is visited:
  → Server renders the page (fetches from API)
  → Caches it statically
  → Subsequent visitors get the cached version instantly

When a new deployment happens:
  → CI pipeline calls POST /api/revalidate-all
  → All existing share pages are re-rendered
  → Cache is warm before any user hits it
```

- **Result:** Share links load at CDN speed, not API speed
- **No stale data:** Cache is invalidated on deployment and on saga completion
- **Open Graph tags** generated per-song for rich link previews in Slack/iMessage/Twitter

---

## Feature 4: Icon Assets for Every Platform

```
Android:   48×48, 72×72, 96×96, 144×144, 192×192 (maskable), 512×512 (maskable)
iOS:       180×180 + Android sizes
Desktop:   SVG favicon
```

- **Maskable icons** support Android 13+ adaptive icons (system applies its own shape)
- Install prompt in compatible browsers shows screenshots of the app

---

## Feature 5: Service Worker (Serwist)

- Configured via Next.js with the Serwist library
- Caches static assets for **offline support**
- If your network drops after the page loads, the app still works
- Background sync potential (queue share requests when offline, send when back online)

---

## Feature 6: PWA Install Banner

- Custom React component that shows an install prompt inside the app UI
- Handles the browser's `beforeinstallprompt` event
- Positioned at the **top on tablet screens** (it was at the bottom, got bumped by keyboard — fixed in issue #19)

---

## The Result

Share a song from Spotify on your phone:

1. Tap Share in Spotify
2. Select "MusicShare" from the share sheet (it's installed on your home screen)
3. App opens, URL pre-filled, resolution starts automatically
4. 2 seconds later: links to YouTube Music and Apple Music

**All of this runs in a web browser. No native code. No App Store.**
