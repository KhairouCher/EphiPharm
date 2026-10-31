# Legacy UI spec, extracted from real screenshots

Source: 18 phone photos of the running app (PHARMA.EXE under DOSBox/VM) sent
2026-10-31. This is DOS text-mode UI (Clipper's own screen painting at the
character-cell level), not a native Windows look — colors below are the
standard 16-color EGA/VGA palette DOS text mode is limited to, which is a
more reliable source than eyeballing glare/reflection in phone photos.
Font was a bitmap VGA font (every character the same cell width).
**Update (2026-10-31, local session with real network access):** the
actual font — Px437 IBM VGA 8x16, a pixel-accurate revival of the real
IBM VGA text-mode bitmap font, CC BY-SA 4.0 — is now bundled in the repo
at `src/Epipharm.App/Resources/Fonts/` and loaded at runtime via
`PrivateFontCollection` (see `RetroTheme.DosFont` and that folder's
`NOTICE.md`), so it renders correctly on any machine the app is copied
to without installing anything system-wide. The earlier fallback to
Consolas/Lucida Console only applies now if that file is somehow
missing from a build.

## Palette (standard DOS 16-color, by role)

| Role | Color | RGB |
|---|---|---|
| Header/footer bar background | Black | 0,0,0 |
| Menu/form body background | Blue | 0,0,170 |
| Browse/list screen background | White (near-white/pale) | 170,170,170 or white |
| Header titles, version, menu labels ("Menu Général") | Bright Cyan | 85,255,255 |
| App name/header text, screen subtitles | Yellow | 255,255,85 |
| Body label text (blue screens) | White | 255,255,255 |
| Therapeutic class / secondary labels | Light Magenta | 255,85,255 |
| Selected menu item / active field | Red background | 170,0,0 |
| Selected item text | White (bold-looking) | 255,255,255 |
| Box borders | White | 255,255,255 |
| Function-key names (`<F1>`, `<Esc>`) | Light Red/Pink | 255,85,85 |
| Function-key descriptions | Cyan or White | 85,255,255 / white |
| Browse-screen text | Blue | 0,0,170 |
| Browse-screen selected row | Red background, white text | 170,0,0 / white |

## Layout, constant across every screen

```
[black] M S P          PRODUITS  PHARMACEUTIQUES          Version 98.1
======================================================================  <- cyan double line
[black] Etablissement : 25120305   DIRECTION DES DOMAINES TEBESSA...
                CHERIA
======================================================================  <- cyan double line
<screen-specific content area>
======================================================================  <- cyan double line (on data screens)
[black] <F1> Help  <F2> Utilisation  <F3> Services  <F4> Maintenance        <Esc> Sortie
```

Two content-area archetypes seen:

**A. Cascading menu (main menu and every submenu)**
Two side-by-side boxed panels on blue background:
- Left: "Menu Général" (cyan label above), white-bordered box, category list
  (e.g. BASE DE DONNEES / DONNEES NATIONALES / DISTRIBUTION /
  APPROVISIONNEMENT / EDITIONS DE GESTION / INVENTAIRES). Current category
  highlighted red.
- Right: "Menu Options" (cyan label above), white-bordered box, the
  highlighted category's items. Current item highlighted red.
- Sometimes a third, smaller floating box overlaps the right panel for a
  further sub-choice (e.g. "Medicaments Usuels / Psychotropes / Autres
  Medicaments" floating over the options list — a 3rd-level pick).

**B. Form screen (data entry)**
- Black subtitle bar under the header naming the current screen
  (e.g. "Livraison sur Bon de Commande (Medicaments)").
- Blue body. Field labels in white, left-aligned, colon-terminated
  ("Code Service :", "No Operation :"). The currently-focused field is a
  red bar (full-width-ish highlight) with the value in white, even when
  empty — this is the blinking-cursor-equivalent in a screenshot.
  Non-focused filled values are yellow or cyan depending on the field's
  role (e.g. codes/numbers often yellow, names/descriptions often cyan).
  A relational lookup value (e.g. the service name after typing its code)
  appears inline in a lighter/different color right after the code field.
- Below the field block, often a grid: white-bordered table with a header
  row (white text on blue-ish header strip) and data rows (white/cyan
  text), e.g. "Libelle DCI/Forme | Stock | Dota. | Q.Liv | Observ.".

**C. Browse/list screen** (seen for DCI structure and dotations lists)
- Pale/near-white or light background instead of blue.
- Column headers and grid lines in blue, data rows in dark blue text.
- Selected row: red background, white text — same convention as the
  cascading menu's selected item.
- A detail strip below the grid shows the full record for the
  currently-selected row (e.g. "C_T : DERMATOLOGIE / DCI : .../ Forme: ...").

## Confirms / extends MENU_MAP.md

- "Menu Général" top-level categories, in order: BASE DE DONNEES,
  DONNEES NATIONALES, DISTRIBUTION, APPROVISIONNEMENT,
  EDITIONS DE GESTION, INVENTAIRES.
- DONNEES NATIONALES options, in order: Classes Thérapeutiques,
  S/Classes Thérapeutiques, Nomenclature Nationale,
  Fichier des Etablissements, Table des Services, Table des Pays,
  Table des Wilayate, Table des Codes Geographiques,
  Table des Secteurs Sanitaires.
- DISTRIBUTION options, in order: Livraison sur Bon de Commande,
  Livraison sur Ordonnance Interne, Ordonnance Stupefiants (Hors
  Dotation), Livraison sur Bon de Souche, Justificatif Stupefiants
  (Tab B), Livraison Exceptionnelle, Livraison ORSEC,
  Transfert Inter-Etablissements, Retour des Produits du Service,
  Duplicata de Bons de Livraison.
- "Livraison sur Bon de Commande" has a 3rd-level choice floating over
  the options list: Medicaments Usuels / Psychotropes / Autres
  Medicaments — confirms deliveries really are split by drug category
  at the UI level too, not just in the data (schema.sql already reflects
  this with drug_category).
- The DCI info screen layout (Code D.C.I, S/Classe, Désignation,
  Forme/Dosage, Date Introduction, Tableau, Stockage, Production,
  Affectation, Retiré, N° Décision, Type Médicament, Saison,
  Appartenance, Date de Retrait) is richer than dci's current schema —
  "Appartenance" shows an 8-digit binary-looking flag (e.g. "11111110"),
  structurally identical to the account rights flags already found in
  USERS.PSW. Worth understanding before building this screen for real.
