using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class ClickDestroySpawn : MonoBehaviour, IPointerClickHandler
{
    [Header("Prefab to create after this is destroyed")]
    public GameObject replacementPrefab;

    [Header("Copy transform to the new object")]
    public bool copyPosition = true;
    public bool copyRotation = true;
    public bool copyScale    = true;
    public bool keepSameParent = true;

    public void OnPointerClick(PointerEventData e)
    {
        // Ensure the click hit this exact GameObject
        if (e.pointerCurrentRaycast.gameObject != gameObject) return;
        SpawnAndDestroy();
    }

    private void SpawnAndDestroy()
    {
        if (replacementPrefab == null)
        {
            Debug.LogWarning("ClickDestroySpawn: replacementPrefab not assigned.");
            return;
        }

        // Cache current transform and parent
        Transform oldParent = transform.parent;
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        Vector3 scl = transform.localScale;

        // Instantiate the replacement first
        GameObject spawned = Instantiate(replacementPrefab);

        if (keepSameParent && oldParent != null)
            spawned.transform.SetParent(oldParent, worldPositionStays: true);

        if (copyPosition) spawned.transform.position = pos;
        if (copyRotation) spawned.transform.rotation = rot;
        if (copyScale)    spawned.transform.localScale = scl;

        // Then remove this object
        Destroy(gameObject);
    }
}
