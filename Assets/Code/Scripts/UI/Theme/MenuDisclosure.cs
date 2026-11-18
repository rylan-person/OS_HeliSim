using UnityEngine;
using UnityEngine.UI;

namespace HeliSim.UI.Theme
{
    /// <summary>
    /// Collapsible list driven by a Toggle (same pattern as the Advanced assists toggle):
    /// the toggle shows or hides the content and turns the chevron to point down when open.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuDisclosure : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private GameObject content;
        [SerializeField] private RectTransform chevron;

        public Toggle Toggle => toggle;
        public GameObject Content => content;

        /// <summary>Configures the disclosure; used by the Editor structure tool and tests.</summary>
        public void Configure(Toggle owner, GameObject list, RectTransform chevronIcon)
        {
            toggle = owner;
            content = list;
            chevron = chevronIcon;
            Refresh();
        }

        private void OnEnable()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.AddListener(HandleChanged);
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(HandleChanged);
            }
        }

        private void HandleChanged(bool _)
        {
            Refresh();
        }

        /// <summary>Shows the content and rotates the chevron to match the toggle.</summary>
        public void Refresh()
        {
            bool open = toggle != null && toggle.isOn;
            if (content != null && content.activeSelf != open)
            {
                content.SetActive(open);
            }

            if (chevron != null)
            {
                chevron.localRotation = Quaternion.Euler(0f, 0f, open ? -90f : 0f);
            }
        }
    }
}
