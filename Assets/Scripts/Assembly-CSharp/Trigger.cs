using System;
using UnityEngine;
using UnityEngine.Events;

public class Trigger : MonoBehaviour
{
    [Serializable]
    public class TriggerEvent : UnityEvent<Collider> {}

    public TriggerEvent triggerEnter;
    public TriggerEvent triggerExit;

    private void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            triggerEnter?.Invoke(col);
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            triggerExit?.Invoke(col);
        }
    }
}
