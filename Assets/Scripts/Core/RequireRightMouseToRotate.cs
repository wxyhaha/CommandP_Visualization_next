using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CommandP.Visualization
{
    [DisallowMultipleComponent]
    public class RequireRightMouseToRotate : MonoBehaviour
    {
        [Tooltip("Mouse button index used to enable rotation (0=left, 1=right, 2=middle)")]
        public int mouseButton = 1;

        [Tooltip("When true, pressing the mouse button restores the original values; when released rotation is disabled.")]
        public bool requireHold = true;

        [Tooltip("If true, also hide the cursor while holding the button")]
        public bool hideCursorWhileHeld = false;

        // store target components and their boolean members
        private struct BoolMember
        {
            public Component component;
            public MemberInfo member;
            public bool originalValue;
        }

        private List<BoolMember> _members = new List<BoolMember>();

        void Awake()
        {
            GatherBooleanRotationMembers();
        }

        void OnValidate()
        {
            // keep inspector changes consistent
            if (Application.isPlaying) return;
            GatherBooleanRotationMembers();
        }

        void GatherBooleanRotationMembers()
        {
            _members.Clear();

            var comps = GetComponents<MonoBehaviour>();
            foreach (var comp in comps)
            {
                if (comp == this) continue;

                var type = comp.GetType();

                // inspect fields
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    if (f.FieldType == typeof(bool) && !f.IsInitOnly)
                    {
                        var name = f.Name.ToLowerInvariant();
                        if (name.Contains("rotation") || name.Contains("rotate") || name.Contains("enablerotation") || name.Contains("enablerotation"))
                        {
                            bool val = (bool)f.GetValue(comp);
                            _members.Add(new BoolMember { component = comp, member = f, originalValue = val });
                        }
                    }
                }

                // inspect properties
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var p in props)
                {
                    if (p.PropertyType == typeof(bool) && p.CanRead && p.CanWrite)
                    {
                        var name = p.Name.ToLowerInvariant();
                        if (name.Contains("rotation") || name.Contains("rotate") || name.Contains("enablerotation") || name.Contains("enablerotation"))
                        {
                            try
                            {
                                bool val = (bool)p.GetValue(comp);
                                _members.Add(new BoolMember { component = comp, member = p, originalValue = val });
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        void Update()
        {
            bool held = Input.GetMouseButton(mouseButton);

            if (hideCursorWhileHeld)
            {
                Cursor.visible = !held;
                Cursor.lockState = held ? CursorLockMode.Locked : CursorLockMode.None;
            }

            foreach (var bm in _members)
            {
                if (bm.component == null || bm.member == null) continue;

                bool shouldEnable = held && requireHold ? bm.originalValue : false;

                if (bm.member is FieldInfo fi)
                {
                    var current = (bool)fi.GetValue(bm.component);
                    if (current != shouldEnable) fi.SetValue(bm.component, shouldEnable);
                }
                else if (bm.member is PropertyInfo pi)
                {
                    try
                    {
                        var current = (bool)pi.GetValue(bm.component);
                        if (current != shouldEnable) pi.SetValue(bm.component, shouldEnable);
                    }
                    catch { }
                }
            }
        }
    }
}
