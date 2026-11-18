using UnityEngine;

namespace HeliSim.UI.Theme
{
    /// <summary>
    /// Shows objects that live outside a page (such as its pinned action bar) only while the page
    /// itself is active. Menu controllers keep activating page roots as before.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuPageAttachment : MonoBehaviour
    {
        [SerializeField] private GameObject[] attached = new GameObject[0];

        public GameObject[] Attached => attached;

        public void Configure(params GameObject[] objects)
        {
            attached = objects ?? new GameObject[0];
            SetAttachedActive(gameObject.activeSelf);
        }

        private void OnEnable()
        {
            SetAttachedActive(true);
        }

        private void OnDisable()
        {
            SetAttachedActive(false);
        }

        private void SetAttachedActive(bool active)
        {
            if (attached == null)
            {
                return;
            }

            for (int i = 0; i < attached.Length; i++)
            {
                if (attached[i] != null && attached[i].activeSelf != active)
                {
                    attached[i].SetActive(active);
                }
            }
        }
    }
}
