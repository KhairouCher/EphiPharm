using System;
using System.Collections.Generic;

namespace Epipharm.App.Theme
{
    /// <summary>
    /// The cascading menu's actual content. Every item here comes from real
    /// evidence — either a screenshot of the running legacy app, or a
    /// string pulled directly out of PHARMA.EXE (see MENU_MAP.md) — except
    /// where a category is built with Confirmed: false, meaning this session
    /// never actually saw that category's real item list and the single
    /// placeholder entry says so rather than inventing items.
    /// </summary>
    public static class MenuData
    {
        public static List<MenuCategory> BuildCategories(Action openStock, Action openReceiptsList,
            Action openReceiptEntry, Action openDeliveriesList, Action<string> openDeliveryEntry,
            Action<string> openDotationEntry, Action openDciInfo, Action openTransferEntry,
            Action<string> openReferenceList, Action<string> showNotBuilt)
        {
            MenuOption Leaf(string name) => new MenuOption(name, () => showNotBuilt(name));

            return new List<MenuCategory>
            {
                // Screenshot confirmed top-level category names and order
                // (UI_SPEC.md). Items below are from MENU_MAP.md's "Stock"
                // and "Allocations / dotations" sections (string-extracted,
                // not a screenshot of this specific category's item list —
                // placement under BASE DE DONNEES is this session's best
                // guess, not confirmed against the running app).
                new MenuCategory("BASE DE DONNEES", new List<MenuOption>
                {
                    new MenuOption("Saisie/Modification du Fichier Stock", openStock),
                    Leaf("Initialisation Stock"),
                    Leaf("Corrections de Stock"),
                    Leaf("Initialisation des Seuils Stocks"),
                    Leaf("Liste des Produits Perimes a Retirer"),
                    new MenuOption("Mise a Jour des Dotations en Medicaments Usuels", () => openDotationEntry("medicament")),
                    new MenuOption("Mise a Jour des Dotations en Psychotropes", () => openDotationEntry("psychotrope")),
                    new MenuOption("Mise a Jour des Dotations Autres Medicaments", () => openDotationEntry("autre")),
                    new MenuOption("Mise a Jour Dotations Stupefiants", () => openDotationEntry("stupefiant")),
                }),

                // Screenshot confirmed (UI_SPEC.md "Confirms / extends
                // MENU_MAP.md"), including order.
                new MenuCategory("DONNEES NATIONALES", new List<MenuOption>
                {
                    new MenuOption("Classes Therapeutiques", () => openReferenceList("classes")),
                    new MenuOption("S/Classes Therapeutiques", () => openReferenceList("subclasses")),
                    new MenuOption("Nomenclature Nationale", openDciInfo),
                    new MenuOption("Fichier des Etablissements", () => openReferenceList("centers")),
                    new MenuOption("Table des Services", () => openReferenceList("services")),
                    new MenuOption("Table des Pays", () => openReferenceList("countries")),
                    new MenuOption("Table des Wilayate", () => openReferenceList("wilayas")),
                    new MenuOption("Table des Codes Geographiques", () => openReferenceList("geocodes")),
                    new MenuOption("Table des Secteurs Sanitaires", () => openReferenceList("sectors")),
                }),

                // Screenshot confirmed, including order and the 3rd-level
                // Medicaments Usuels/Psychotropes/Autres Medicaments popup
                // on the first item.
                new MenuCategory("DISTRIBUTION", new List<MenuOption>
                {
                    new MenuOption("Livraison sur Bon de Commande", subOptions: new List<MenuOption>
                    {
                        new MenuOption("Medicaments Usuels", () => openDeliveryEntry("medicament")),
                        new MenuOption("Psychotropes", () => openDeliveryEntry("psychotrope")),
                        new MenuOption("Autres Medicaments", () => openDeliveryEntry("autre")),
                    }),
                    Leaf("Livraison sur Ordonnance Interne"),
                    Leaf("Ordonnance Stupefiants (Hors Dotation)"),
                    Leaf("Livraison sur Bon de Souche"),
                    Leaf("Justificatif Stupefiants (Tab B)"),
                    Leaf("Livraison Exceptionnelle"),
                    Leaf("Livraison ORSEC"),
                    new MenuOption("Transfert Inter-Etablissements", openTransferEntry),
                    Leaf("Retour des Produits du Service"),
                    Leaf("Duplicata de Bons de Livraison"),
                    // From MENU_MAP.md section 2 (string-extracted, not
                    // seen in a DISTRIBUTION screenshot) — appended here
                    // rather than inserted among the screenshot-confirmed
                    // items above, so this item's position isn't claimed
                    // as confirmed, only its existence and wording.
                    new MenuOption("Liste Globale des Bons de Livraison/Restitution", openDeliveriesList),
                }),

                // From MENU_MAP.md's "Orders" and "Supplier receipts"
                // sections (string-extracted) — grouping and exact order
                // within this category not screenshot-confirmed.
                new MenuCategory("APPROVISIONNEMENT", new List<MenuOption>
                {
                    Leaf("Edition Commande"),
                    Leaf("Liste des Commandes"),
                    Leaf("Annulation Commande"),
                    new MenuOption("Reception de Produits", openReceiptEntry),
                    Leaf("Duplicata de Bon de Reception"),
                    new MenuOption("Liste Globale des Bons de Reception", openReceiptsList),
                }),

                // From MENU_MAP.md's "Reports / listings" section
                // (string-extracted) — exact order not confirmed.
                new MenuCategory("EDITIONS DE GESTION", new List<MenuOption>
                {
                    Leaf("Medicaments en Rupture (Etat Previsionnel)"),
                    Leaf("Etat des DCI Strategiques"),
                    Leaf("Etat des Livraisons Detaillees"),
                    Leaf("Etat Recapitulatif par Produit"),
                    Leaf("Etat par Classes Therapeutiques"),
                    Leaf("Editions par Service"),
                    Leaf("Editions Structure"),
                }),

                // Genuinely not recovered: only scattered strings
                // ("Inventaire No:", "Edition du:"), no real item list seen.
                new MenuCategory("INVENTAIRES", new List<MenuOption>
                {
                    new MenuOption("(menu non releve - a verifier sur l'application reelle)", confirmed: false),
                }),
            };
        }
    }
}
