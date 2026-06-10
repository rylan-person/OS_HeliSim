using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Bimicore.Lib
{
    public class ComponentGroup : MonoBehaviour
    {
        public string groupName;

        public bool visibility;
        public bool isEditing;

        public ComponentGroup parent;
        public List<Component> comps = new List<Component>();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ComponentGroup))]
    public class ComponentGroupEditor : Editor
    {
        private ComponentGroup _componentGroup;

        private List<Component> _allComponents;
        private List<ComponentGroup> _allComponentGroups;

        private Color _standardElementColor;

        private static int _numOfGroupsToCollapseAtFirstLaunch = 0;

        private void Awake()
        {
            _standardElementColor = GUI.color;

            _componentGroup = (ComponentGroup) target;

            UpdateGroupData();

            CollapseGroupAtFirstLaunch();
        }

        /// <summary>
        /// Collapse group only on FIRST launch (for example, after launch Unity engine).
        /// Since there is no method that tracks only the FIRST run,
        /// we use a static variable that counts the number of all Awake() methods run
        /// </summary>
        private void CollapseGroupAtFirstLaunch()
        {
            if (_numOfGroupsToCollapseAtFirstLaunch >= _allComponentGroups.Count)
                return;

            SetGroupVisibility(_componentGroup, false);
            _componentGroup.isEditing = false;

            _numOfGroupsToCollapseAtFirstLaunch++;
        }

        private void OnEnable()
        {
            for (int i = _componentGroup.comps.Count - 1; i >= 0; i--)
            {
                TryRemoveNullComponents(_componentGroup, _componentGroup.comps[i]);
            }
        }

        private static bool TryRemoveNullComponents(ComponentGroup group, Component component)
        {
            if (component != null)
                return false;

            group.comps.Remove(component);

            return true;
        }

        private static void SetGroupVisibility(ComponentGroup group, bool visibility)
        {
            for (int i = group.comps.Count - 1; i >= 0; i--)
            {
                var component = group.comps[i];

                if (TryRemoveNullComponents(group, component))
                    continue;

                // Change visibility of child groups if they exist
                if (component is ComponentGroup childGroup)
                {
                    // If you collapse the parent group, then the next time you expand the parent group,
                    // the child groups will be collapsed
                    SetGroupVisibility(childGroup, false);
                }

                SetComponentVisibility(component, visibility);
            }

            group.visibility = visibility;
        }

        private static void SetComponentVisibility(Component component, bool visibility)
        {
            if (visibility)
            {
                component.hideFlags &= ~HideFlags.HideInInspector;
                // required if the object was deselected in between
                Editor.CreateEditor(component);
            }
            else
            {
                component.hideFlags |= HideFlags.HideInInspector;
            }
        }

        public override void OnInspectorGUI()
        {
            if (_componentGroup.isEditing)
            {
                // TODO: heavy operation. Better update data only when they could change
                UpdateGroupData();

                Draw_EditMode();
            }
            else
            {
                Draw_StandardMode();
            }
        }

        private void UpdateGroupData()
        {
            _allComponents = _componentGroup.gameObject.GetComponents<Component>().ToList();
            _allComponentGroups = _componentGroup.gameObject.GetComponents<ComponentGroup>().ToList();

            SetParentForChildGroups();
        }

        private void SetParentForChildGroups()
        {
            foreach (var componentGroup in _allComponentGroups)
            {
                foreach (var component in componentGroup.comps)
                {
                    if (component is ComponentGroup group)
                    {
                        group.parent = componentGroup;
                    }
                }
            }
        }

        private void Draw_EditMode()
        {
            var availableComponents = GetAvailableComponentsIn(_allComponents);

            GUILayout.BeginHorizontal();

            Draw_GroupNameInputBox();
            Draw_DoneButton();

            GUILayout.EndHorizontal();

            Draw_AvailableComponents(availableComponents);
        }

        private List<Component> GetAvailableComponentsIn(List<Component> components)
        {
            components.Remove(_componentGroup); // Remove from list current group (which we are editing now)
            RemoveParentsFromAvailableComponents(ref components);
            RemoveOtherChildrenFromAvailableComponents(ref components);

            return components;
        }

        private void RemoveParentsFromAvailableComponents(ref List<Component> components)
        {
            var tempParent = _componentGroup.parent;

            while (tempParent != null)
            {
                components.Remove(tempParent);
                tempParent = tempParent.parent;
            }
        }

        /// <summary>
        /// Remove components that belong to other component groups
        /// </summary>
        private void RemoveOtherChildrenFromAvailableComponents(ref List<Component> components)
        {
            foreach (var group in _allComponentGroups)
            {
                if (group != _componentGroup)
                    components = components.Except(group.comps).ToList();
            }
        }

        private void Draw_GroupNameInputBox()
        {
            _componentGroup.groupName = GUILayout.TextField(_componentGroup.groupName);
        }

        private void Draw_DoneButton()
        {
            if (GUILayout.Button("Done", GUILayout.Width(40)))
            {
                _componentGroup.isEditing = false;
            }
        }

        private void Draw_AvailableComponents(List<Component> components)
        {
            foreach (var component in components)
            {
                string componentName = GetComponentName(component);

                Draw_ComponentToggle(component, componentName);
            }
        }

        private static string GetComponentName(Component component)
        {
            string componentName = component.GetType().Name;

            if (component is ComponentGroup group)
                componentName = "COMPONENT GROUP(" + group.groupName + ")";

            return componentName;
        }

        private void Draw_ComponentToggle(Component component, string componentName)
        {
            bool isInList = _componentGroup.comps.Contains(component);
            GUI.color = isInList ? Color.green : _standardElementColor;

            var componentToggle = GUILayout.Toggle(isInList, componentName);

            if (TryChangeToggleValue(componentToggle, isInList))
            {
                if (isInList)
                    RemoveComponentFromGroup(component);

                else
                    AddComponentToGroup(component);
            }
        }

        private static bool TryChangeToggleValue(bool toggle, bool value) => toggle != value;

        private void RemoveComponentFromGroup(Component component)
        {
            _componentGroup.comps.Remove(component);

            if (component is ComponentGroup group)
            {
                group.parent = null;
            }
        }

        private void AddComponentToGroup(Component component)
        {
            _componentGroup.comps.Add(component);
        }

        private void Draw_StandardMode()
        {
            GUILayout.BeginHorizontal();

            Draw_CollapseOrExpandButton();
            Draw_EditButton();

            GUILayout.EndHorizontal();
        }

        private void Draw_CollapseOrExpandButton()
        {
            GUI.color = _componentGroup.visibility ? Color.green : _standardElementColor;

            if (GUILayout.Button(_componentGroup.groupName))
                SetGroupVisibility(_componentGroup, !_componentGroup.visibility);
        }

        private void Draw_EditButton()
        {
            GUI.color = _standardElementColor;

            if (GUILayout.Button("Edit", GUILayout.Width(40)))
            {
                SetGroupVisibility(_componentGroup, true);
                _componentGroup.isEditing = true;
            }
        }
    }
#endif
}