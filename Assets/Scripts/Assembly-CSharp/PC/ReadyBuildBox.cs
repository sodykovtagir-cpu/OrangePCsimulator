using UnityEngine;

namespace PC
{
    /// <summary>
    /// Issue a ready-build plan or an item when its crate is opened.
    /// Debris and contents use normal physics contacts for every crate.
    /// </summary>
    public class ReadyBuildBox : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Что выдать при вскрытии ящика")]
        private GameObject prefab;

        [SerializeField]
        [Tooltip("Смещение относительно ящика")]
        private Vector3 position;

        [SerializeField]
        [Tooltip("Доворот относительно ящика")]
        private Vector3 rotation;

        private void Start()
        {
            if (prefab == null) return;

            var obj = Instantiate(prefab, transform.position, transform.rotation);
            if (obj == null) return;

            obj.transform.Translate(position);
            obj.transform.Rotate(rotation);
        }
    }
}
