// Compile-only engine doubles for Box / CustomPaint / ModForge delivery tests.
// Not proof of a Unity build or of rendered layout.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }

    public class Object
    {
        public string name;
        public static readonly List<Object> Destroyed = new List<Object>();
        public static int InstantiateCount;
        public static Vector3 LastInstantiatePosition;
        public static GameObject Instantiate(GameObject source, Vector3 p, Quaternion q)
        {
            InstantiateCount++;
            LastInstantiatePosition = p;
            if (source == null) return null;
            var clone = new GameObject((source.name ?? "clone") + "_copy") { Template = source };
            clone.CopyComponentsFrom(source);
            clone.CopyChildrenFrom(source);
            return clone;
        }
        public static void Destroy(Object o) { Destroyed.Add(o); }
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform { get { return gameObject != null ? gameObject.transform : null; } }
        public T GetComponent<T>() where T : Component { return gameObject != null ? gameObject.GetComponent<T>() : null; }
        public T GetComponentInChildren<T>() where T : Component { return GetComponent<T>(); }
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component
        {
            return gameObject != null ? gameObject.GetComponentInChildren<T>(includeInactive) : null;
        }
    }

    public class MonoBehaviour : Component { }

    public class Transform : Component
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localPosition;
        public int translateCalls, rotateCalls;
        public void Translate(Vector3 v) { translateCalls++; localPosition = v; }
        public void Rotate(Vector3 v) { rotateCalls++; }
    }

    public class GameObject : Object
    {
        private readonly Dictionary<Type, Component> parts = new Dictionary<Type, Component>();
        public readonly List<GameObject> Children = new List<GameObject>();
        public bool activeSelf = true;
        public GameObject Template;
        public Transform transform;
        public GameObject(string n) { name = n; transform = new Transform { gameObject = this, name = n }; }
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this, name = name };
            parts[typeof(T)] = c;
            return c;
        }
        public T GetComponent<T>() where T : Component
        {
            Component c;
            return parts.TryGetValue(typeof(T), out c) ? (T)c : null;
        }
        public System.Collections.Generic.IEnumerable<Component> All { get { return parts.Values; } }
        public void AddChild(GameObject child) { Children.Add(child); }
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component
        {
            var own = GetComponent<T>();
            if (own != null) return own;
            foreach (var child in Children)
            {
                if (!includeInactive && !child.activeSelf) continue;
                var found = child.GetComponentInChildren<T>(includeInactive);
                if (found != null) return found;
            }
            return null;
        }
        public void CopyChildrenFrom(GameObject source)
        {
            foreach (var child in source.Children)
            {
                var copy = new GameObject(child.name) { activeSelf = child.activeSelf };
                copy.CopyComponentsFrom(child);
                copy.CopyChildrenFrom(child);
                Children.Add(copy);
            }
        }

        public void CopyComponentsFrom(GameObject source)
        {
            foreach (var type in new System.Collections.Generic.List<Type>(source.parts.Keys))
            {
                var c = (Component)Activator.CreateInstance(type);
                c.gameObject = this;
                c.name = name;
                parts[type] = c;
            }
        }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
    }
    public struct Quaternion { }

    public class Renderer : Component { public Material material = new Material(); }
    public class Material : Object { public Texture mainTexture; }
    public class Texture : Object { }
    public class Texture2D : Texture
    {
        public int width, height;
        public bool readOnly;
        public Texture2D(int w, int h) { width = w; height = h; }
        public byte[] EncodeToPNG() { return new byte[] { 1, 2, 3 }; }
        public void Apply(bool updateMipmaps = true, bool makeNoLongerReadable = false) { readOnly = makeNoLongerReadable; }
    }
    public class Crate : Component
    {
        // Mirrors Crate.cs: корень — целый ящик, broken — выключенная сборка
        // с Box, которая включается ударом молотка.
        public GameObject broken;
        public bool Opened { get { return broken != null && broken.activeSelf; } }
        public bool CanOpen() { return broken != null; }
        public void BreakApart() { Open(); }
        public void Open()
        {
            if (broken == null) return;
            broken.activeSelf = true;
        }
    }

    public static class Debug
    {
        public static readonly List<string> Warnings = new List<string>();
        public static void LogWarning(string s) { Warnings.Add(s); }
        public static void Log(string s) { }
    }
}

namespace Newtonsoft.Json.Linq
{
    public enum JTokenType { Null, String }
    public class JToken { public JTokenType Type; public static JToken FromObject(object o) { return new JToken(); } public static implicit operator string(JToken t) { return null; } }
    public class JObject { public JToken this[string key] { get { return null; } set { } } public void Add(string k, JToken v) { } }
}

public static class FormatConverter
{
    public static UnityEngine.Texture2D BytesToTexture(byte[] data, bool mipmaps) { return new UnityEngine.Texture2D(1, 1); }
}

// Save contract double (real interface lives in the Unity assembly).
public interface ISave
{
    void ToData(Newtonsoft.Json.Linq.JObject jObject);
    void FromData(Newtonsoft.Json.Linq.JObject jObject);
}
