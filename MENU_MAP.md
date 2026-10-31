# EPIPHARM — recovered menu / screen map

Recovered from the French UI strings embedded in `PHARMA.EXE` (Clipper 5.01).
This is text I pulled from the binary, not a decompile of the logic — it tells
us what screens and labels exist, not exactly how each one behaves. Treat it
as a first draft to confirm against the running app (DOSBox-X) or with staff,
not as ground truth. Every item below is a direct or lightly-cleaned quote
from the executable; nothing is invented.

Header text found: "Ministere de la Sante et de la Population", address
"125, Bd Abderrahmane LAALA - El Madania - ALGER", contact
"Assistance Telephonique : 27.94.57", app title
"PRODUITS PHARMACEUTIQUES Version 98.1".

## 1. Stock (Fichier Stock)
- Saisie/Modification du Fichier Stock (create/edit a stock record)
- Initialisation Stock
- Corrections de Stock
- Initialisation des Seuils Stocks (minimum/safety/reorder thresholds)
- Liste des Produits Perimes a retirer Immediatement du Stock (expired items to pull)
- Liste des D.C.I a Retirer du Stock
- Rules observed: a product can only enter stock through a supplier receipt
  ("Produit ne peut etre saisi que par une reception fournisseur"); quantity
  must be positive; a product already moved this period can't be re-entered.

## 2. Deliveries to hospital services (Livraisons)
- Livraison sur Bon de Commande (Medicaments)
- Livraison sur Bon de Commande (Psychotropes)
- Livraison sur Bon de Commande (Autres Medicaments)
- Livraison sur Ordonnance Interne
- Livraison sur Ordonnance Stupefiant
- Livraison sur Bon de Souche (Stupefiants)
- Livraison Exceptionnelle (Medicaments / Psychotropes / Autres Medicaments)
- Livraison Exceptionnelle (ORSEC) — emergency/disaster-plan deliveries
- Duplicata de Bon de Livraison/Restitution
- Liste Globale des Bons de Livraison/Restitution
- So deliveries are split by drug category (ordinary / psychotropic /
  controlled-narcotic / other) at every step — this distinction needs to
  carry through the rebuild's data model and permissions, not just the UI.

## 3. Supplier receipts (Receptions)
- Reception de Produits
- Duplicata de Bon de Reception
- Liste Globale des Bons de Reception
- A product already received this period can't be received again
  ("Produits Existe Deja Mouvemente").

## 4. Orders (Commandes)
- Edition Commande
- Liste des Commandes
- Annulation Commande
- An order can be "Lancee" (issued) or "Annulee" (cancelled); a cancelled or
  not-yet-issued order is rejected if a movement references it.

## 5. Allocations / dotations
- Mise a Jour des Dotations en Medicaments Usuels
- Mise a Jour des Dotations en Psychotropes
- Mise a Jour des Dotations Autres Medicaments
- Mise a Jour Dotations Stupefiants
- Saisie/Modification des Seuils
- ETAT des DOTATIONS (one report per category above) + ETAT DES DOTATIONS
  (Tableau B) — "Tableau B" is the Algerian controlled-substances schedule,
  so some DCI entries are flagged as belonging to it
  ("Cette Dci Fait/ne Fait pas Partie du Tableau B").
- A dotation can't be deleted once "justifiee" (accounted for), and deleting
  it is blocked if movements exist against it.

## 6. Reference data (nomenclature) — all with add/edit/list/delete screens
- DCI (generic drug) — "Denomination Commune Internationale",
  "NOMENCLATURE DES MEDICAMENTS (DCI Structure)"
- DCI Orsec (emergency-plan drug list), DCI Strategiques
- Produits (commercial products) — code, supplier, label, form, local/foreign
  marketing-authorization numbers
- Formes (dosage forms) — sigle, dosage, unite, designation
- Classes Therapeutiques / Sous-Classes Therapeutiques
- Fournisseurs (suppliers), Laboratoires
- Pays (countries), Wilaya (provinces), Secteurs Sanitaires, Centres,
  Codes Geographiques — Algeria's administrative hierarchy for hospital
  catchment areas
- Services (hospital wards/departments) — code, address, bed counts
  (civil/military — "NBRE_LITT"/"NBRE_LITO"), category, phone/telex/fax
- Medecins (doctors) — linked to a service, with "Depart" (departure/leaving)
  tracked separately from deletion

## 7. Reports / listings (Etats et Listings)
- Medicaments en Rupture (Etat Previsionnel) — projected stock-outs
- Etat des DCI Strategiques
- Etat des Livraisons detailles / Etat Recapitulatif par Produit /
  Etat par Classes Therapeutiques — by service, and "Editions Structure"
  (facility-wide), can be printed or sent to a file
  ("Choix destination Edition I(mprimante) F(ichier)")
- Inventaire (stock take) with its own detailed/summary states
- Reports are locked to a single year at a time.

## 8. Year-end archive
- "ATTENTION: Archivage des donnees de l'annee: ... non fait" — warns if the
  year hasn't been archived yet. This is what produces the `MV/LV/CR/RE20xx.ARC`
  files found in the folder, and it reorganizes the DCI file afterward
  ("Reorganisation du fichier DCI en cours").

## 9. System / admin
- Parametres d'Exploitation (the single-row settings record, `PARPHARM.DBF`)
- Users: "Saisie et Mise a Jour des Fournisseurs/Laboratoires" style
  create/edit screens exist generally across modules; user accounts are
  separate (`USERS.PSW`, handled already — see accounts work).
- A recurring confirmation step before destructive actions:
  "Confirmation (O/N/Esc)?", with Escape always available to back out.

## UI conventions worth carrying into the rebuild
- Every data-entry screen shows its hotkeys at the bottom, consistently:
  `<F1> Help`, `<F2> Menu d'utilisation`, `<F5> List (lookup)`,
  `<F10>` secondary list, `<I>` print, `<Esc> Sortie` (exit/cancel).
  This is why staff who "barely type" can still use it — almost nothing
  requires typing a full value; F5 opens a pick-list instead.
- Consistent validation messages: "<field> Obligatoire" (required),
  "<thing> Inexistant(e)" (not found), "<thing> existe deja" (duplicate),
  "La Quantite doit etre Positive" (must be positive). The rebuild should
  reuse this exact wording so error messages don't feel foreign to staff.
- Record lists support tagging/multi-select: `<SP> Marquer/Liberer`,
  `<M> Marquer Tout`, `<L> Liberer Tout` — select-all / select-none on a list,
  then act on the selection (used for things like batch printing).

## What's still unconfirmed
- Exact field-level layout of each entry screen (I have labels, not
  positions) — best gathered from screenshots of the real app.
- What the 8-position `TDROIT` permission flags individually control
  (only position 0, admin-or-not, is confirmed from the accounts work).
- Whether "Editions par Service" vs "Editions Structure" is a hard
  permission boundary or just a report filter.
