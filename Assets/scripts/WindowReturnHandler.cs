using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WindowReturnHandler : MonoBehaviour
{
    private EndSceneController owner;
    private Collider2D cachedCollider;
    private bool interactable = false;

    void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
        if (cachedCollider != null)
            cachedCollider.enabled = false;
    }

    void Update()
    {
        if (!interactable || owner == null) return;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryHandlePointer(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        }
        else if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            TryHandlePointer(UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue());
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            TryHandlePointer(Input.mousePosition);
        }
        else if (Input.touchSupported && Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TryHandlePointer(Input.GetTouch(0).position);
        }
#endif
    }

    public void Configure(EndSceneController controller, bool enable)
    {
        owner = controller;
        interactable = enable;
        if (cachedCollider != null)
            cachedCollider.enabled = enable;
    }

    void TryHandlePointer(Vector2 screenPos)
    {
        if (cachedCollider == null) return;
        if (UnityEngine.Camera.main == null) return;

        Vector2 worldPoint = UnityEngine.Camera.main.ScreenToWorldPoint(screenPos);
        if (cachedCollider.OverlapPoint(worldPoint))
        {
            owner.HandleWindowReturn();
        }
    }
}
