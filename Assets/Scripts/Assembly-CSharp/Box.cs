using UnityEngine;

public class Box : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector3 position;
    [SerializeField] private Vector3 rotation;

    private void Start()
    {
        if (prefab == null) return;
        var obj = Instantiate(prefab, transform.position, transform.rotation);
        if (obj != null)
        {
            var tr = obj.transform;
            tr.Translate(position);
            tr.Rotate(rotation);
        }
    }
}
