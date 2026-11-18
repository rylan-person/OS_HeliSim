using HeliSim.InFlightMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliSim.UI.Theme
{
    /// <summary>
    /// Settings action bar: shows whether the page has unapplied changes. Apply and Revert keep
    /// their existing listeners; the page presenters still decide when Apply is interactable.
    /// Polls <see cref="InFlightMenuPage.IsDirty"/> and only writes when the value changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuActionBar : MonoBehaviour
    {
        public const string DirtyText = "Unsaved changes";
        public const string CleanText = "All changes applied";

        [SerializeField] private MenuTheme theme;
        [SerializeField] private InFlightMenuPage page;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Graphic stateDot;
        [Tooltip("Apply and Revert: interactable only while the page has unsaved changes.")]
        [SerializeField] private CanvasGroup[] dirtyOnly = new CanvasGroup[0];

        private bool hasState;
        private bool lastDirty;

        public InFlightMenuPage Page => page;

        /// <summary>Configures the bar; used by the Editor structure tool and tests.</summary>
        public void Configure(MenuTheme menuTheme, InFlightMenuPage owner, TMP_Text label, Graphic dot,
            params CanvasGroup[] dirtyOnlyGroups)
        {
            theme = menuTheme;
            page = owner;
            stateLabel = label;
            stateDot = dot;
            dirtyOnly = dirtyOnlyGroups ?? new CanvasGroup[0];
            hasState = false;
            Refresh();
        }

        private void OnEnable()
        {
            hasState = false;
            Refresh();
        }

        private void LateUpdate()
        {
            bool dirty = page != null && page.IsDirty;
            if (!hasState || dirty != lastDirty)
            {
                Refresh();
            }
        }

        /// <summary>Updates the state text and dot from the page.</summary>
        public void Refresh()
        {
            bool dirty = page != null && page.IsDirty;
            hasState = true;
            lastDirty = dirty;
            MenuPalette palette = MenuTheme.PaletteOf(theme);
            if (stateLabel != null)
            {
                stateLabel.text = dirty ? DirtyText : CleanText;
                stateLabel.color = dirty ? palette.text1 : palette.text2;
            }

            if (stateDot != null)
            {
                stateDot.color = dirty ? palette.accent : palette.ok;
            }

            if (dirtyOnly != null)
            {
                for (int i = 0; i < dirtyOnly.Length; i++)
                {
                    if (dirtyOnly[i] != null)
                    {
                        dirtyOnly[i].interactable = dirty;
                    }
                }
            }
        }
    }
}
