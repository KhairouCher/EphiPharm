# Font notice

`Px437_IBM_VGA_8x16.ttf` is **Px437 IBM VGA 8x16**, a pixel-accurate
revival of the real IBM VGA text-mode bitmap font — the actual font the
legacy EPIPHARM (Clipper 5.01 DOS) screens used — from VileR's
**Oldschool PC Font Pack v2.2**:
<https://int10h.org/oldschool-pc-fonts/>

License: **CC BY-SA 4.0** — <https://creativecommons.org/licenses/by-sa/4.0/>
(attribution + share-alike; bundling the unmodified font file in this
app is permitted, this notice is the attribution).

This replaces the earlier fallback to Consolas/Lucida Console noted in
UI_SPEC.md — that fallback couldn't be avoided from a cloud session with
no network access to fetch the real font; this is a regular local
session with normal internet access, so it's fetched for real now.
`RetroTheme.DosFont` (src/Epipharm.App/Theme/RetroTheme.cs) loads this
file directly via `PrivateFontCollection`, so it renders correctly on
any machine that runs the app, without installing anything system-wide.
