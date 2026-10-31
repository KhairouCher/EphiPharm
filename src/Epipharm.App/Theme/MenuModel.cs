using System;
using System.Collections.Generic;

namespace Epipharm.App.Theme
{
    /// <summary>
    /// One leaf of the cascading menu. Action runs when chosen; if
    /// SubOptions is non-empty, choosing it instead opens a third-level
    /// floating list first (see UI_SPEC.md "Livraison sur Bon de Commande"'s
    /// Medicaments Usuels/Psychotropes/Autres Medicaments popup) — the
    /// chosen sub-option's own Action then runs.
    /// </summary>
    public class MenuOption
    {
        public string Name { get; }
        public Action OnChosen { get; }
        public List<MenuOption> SubOptions { get; }

        /// <summary>True for items pulled straight from a screenshot or the
        /// executable's own strings; false for a category whose real item
        /// list this session never actually saw and is left as a clearly
        /// marked placeholder rather than invented.</summary>
        public bool Confirmed { get; }

        public MenuOption(string name, Action onChosen = null, List<MenuOption> subOptions = null, bool confirmed = true)
        {
            Name = name;
            OnChosen = onChosen;
            SubOptions = subOptions ?? new List<MenuOption>();
            Confirmed = confirmed;
        }
    }

    public class MenuCategory
    {
        public string Name { get; }
        public List<MenuOption> Options { get; }

        public MenuCategory(string name, List<MenuOption> options)
        {
            Name = name;
            Options = options;
        }
    }
}
