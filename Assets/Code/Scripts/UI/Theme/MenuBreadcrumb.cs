using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliSim.UI.Theme
{
    /// <summary>
    /// Top-bar breadcrumb ("Main menu / Fly", "Settings / Display &amp; graphics"). The parent crumb
    /// is a button only when it leads somewhere; menu controllers call <see cref="Show"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuBreadcrumb : MonoBehaviour
    {
        [SerializeField] private MenuTheme theme;
        [SerializeField] private string rootText = "Main menu";
        [SerializeField] private Button parentButton;
        [SerializeField] private TMP_Text parentLabel;
        [SerializeField] private TMP_Text currentLabel;

        public string CurrentText => currentLabel != null ? currentLabel.text : string.Empty;
        public string ParentText => parentLabel != null ? parentLabel.text : string.Empty;

        public void Configure(MenuTheme menuTheme, string root, Button parent, TMP_Text parentText, TMP_Text current)
        {
            theme = menuTheme;
            rootText = root;
            parentButton = parent;
            parentLabel = parentText;
            currentLabel = current;
            Show(current != null ? current.text : string.Empty);
        }

        /// <summary>Shows the current page; a non-empty parent turns the first crumb into a link.</summary>
        public void Show(string current, string parent = null)
        {
            bool link = !string.IsNullOrEmpty(parent);
            MenuPalette palette = MenuTheme.PaletteOf(theme);
            if (parentLabel != null)
            {
                parentLabel.text = link ? parent : rootText;
                parentLabel.color = link ? palette.text2 : palette.text3;
            }

            if (parentButton != null)
            {
                parentButton.interactable = link;
            }

            if (currentLabel != null)
            {
                currentLabel.text = current ?? string.Empty;
            }
        }
    }
}
