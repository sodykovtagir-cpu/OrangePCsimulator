#if UNITY_EDITOR
using PC.Component.Cabling;
using UnityEditor;
using UnityEngine;

// Run in Play Mode when a cable visibly clips through a floor or case.
// Read-only diagnostics: never changes saved ropes or physics settings.
public static class CableCollisionDiagnostics
{
    [MenuItem("Tools/OrangePC/Diagnose cable collisions now")]
    private static void Diagnose()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Cable collision check: enter Play Mode first.");
            return;
        }

        Physics.SyncTransforms();
        var cables = Object.FindObjectsOfType<CableItem>();
        var hits = new Collider[64];
        foreach (var cable in cables)
        {
            int sections = 0, disabled = 0, overlaps = 0, ignored = 0, below = 0;
            string example = "none";
            foreach (var body in cable.GetComponentsInChildren<Rigidbody>())
            {
                if (!body.name.StartsWith("Cable segment ")) continue;
                var capsule = body.GetComponent<CapsuleCollider>();
                if (capsule == null) continue;
                sections++;
                if (!body.detectCollisions || !capsule.enabled || body.isKinematic) disabled++;
                int count = Physics.OverlapSphereNonAlloc(body.position,
                    Mathf.Max(.4f, capsule.height * .5f + .25f), hits, ~0,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var other = hits[i];
                    if (other == null || other == capsule ||
                        other.GetComponentInParent<CableItem>() == cable) continue;
                    Vector3 direction;
                    float depth;
                    if (!Physics.ComputePenetration(capsule, body.position,
                        body.rotation, other, other.transform.position,
                        other.transform.rotation, out direction, out depth) ||
                        depth < .01f) continue;
                    overlaps++;
                    bool excluded = Physics.GetIgnoreCollision(capsule, other) ||
                        Physics.GetIgnoreLayerCollision(capsule.gameObject.layer,
                            other.gameObject.layer);
                    if (excluded) ignored++;
                    if (example == "none")
                        example = body.name + " / " + other.name + " depth=" +
                            depth.ToString("0.000") + "m ignored=" + excluded;
                }
                // A completely submerged capsule no longer overlaps a thin
                // floor collider. Report solids above its centre as a clue.
                RaycastHit above;
                if (Physics.Raycast(body.position + Vector3.up * .5f,
                    Vector3.down, out above, .5f, ~0,
                    QueryTriggerInteraction.Ignore) &&
                    above.collider.GetComponentInParent<CableItem>() != cable &&
                    above.point.y - body.position.y > capsule.radius + .02f)
                    below++;
            }
            Debug.Log("Cable collision check: " + cable.name + " id=" + cable.Id +
                " sections=" + sections + " disabled=" + disabled +
                " penetrations=" + overlaps + " ignored=" + ignored +
                " below-solid-suspects=" + below + " example=" + example);
        }
        Debug.Log("Cable collision check: finished " + cables.Length + " cables. " +
            "If sections overlap visible objects but penetrations=0, check " +
            "whether those objects have solid Colliders in the scene.");
    }
}
#endif
