using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Click : MonoBehaviour, IPointerClickHandler
{
    public enum SpawnMode
    {
        AtThisObject,      // this GameObject's position
        AtWorldPosition,   // a fixed Vector3
        AtPointerWorld     // mouse/touch world position
    }

    [Serializable]
    public class SpawnItem
    {
        public string name;                 // optional label for clarity
        public GameObject prefab;           // what to spawn
        public SpawnMode mode = SpawnMode.AtThisObject;
        public Vector3 worldPosition;       // used if mode == AtWorldPosition
        public Vector3 offset;              // applied after base position
        public Transform parent;            // optional parent
        public bool copyRotation = false;   // use this object's rotation
        public bool copyScale = false;      // use this object's localScale
        public float zRotation = 0f;        // manual z rotation if not copying rotation
        public bool enabled = true;         // toggle this item on/off
    }

    [Header("Spawn set for this click")]
    public List<SpawnItem> items = new List<SpawnItem>();

    [Header("Destroy the clicked object after spawning?")]
    public bool destroyThisOnClick = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject != gameObject) return;

        Camera cam = Camera.main;

        foreach (var item in items)
        {
            if (item == null || !item.enabled) continue;
            if (item.prefab == null)
            {
                Debug.LogWarning("Click: A SpawnItem has no prefab assigned.");
                continue;
            }

            // Resolve base position per item
            Vector3 basePos;
            switch (item.mode)
            {
                case SpawnMode.AtWorldPosition:
                    basePos = item.worldPosition;
                    break;

                case SpawnMode.AtPointerWorld:
                    if (cam == null)
                    {
                        Debug.LogWarning("Click: No Main Camera found for AtPointerWorld mode.");
                        basePos = transform.position;
                    }
                    else
                    {
                        basePos = cam.ScreenToWorldPoint(eventData.position);
                        basePos.z = transform.position.z; // same plane as the clicked object
                    }
                    break;

                case SpawnMode.AtThisObject:
                default:
                    basePos = transform.position;
                    break;
            }

            Vector3 finalPos = basePos + item.offset;

            // Rotation per item
            Quaternion rot = item.copyRotation ? transform.rotation
                                               : Quaternion.Euler(0f, 0f, item.zRotation);

            // Instantiate
            GameObject go = Instantiate(item.prefab, finalPos, rot);

            // Parent per item
            if (item.parent != null)
                go.transform.SetParent(item.parent, worldPositionStays: true);

            // Scale per item
            if (item.copyScale)
                go.transform.localScale = transform.localScale;
        }

        if (destroyThisOnClick)
            Destroy(gameObject);
    }
}
