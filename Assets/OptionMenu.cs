using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class OptionMenu : MonoBehaviour
{
    [SerializeField] private Button option1Btn;
    [SerializeField] private Button option2Btn;
    [SerializeField] private TMPro.TextMeshProUGUI option1Label;
    [SerializeField] private TMPro.TextMeshProUGUI option2Label;

    RectTransform rt;
    Canvas rootCanvas;

    void Awake()
    {
        rt = transform as RectTransform;
        rootCanvas = GetComponentInParent<Canvas>();
        if (!option1Label) option1Label = option1Btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (!option2Label) option2Label = option2Btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
    }

    public void ShowAtScreen(
        Vector2 screenPos,
        string label1, UnityAction action1,
        string label2, UnityAction action2)
    {
        Clear();
        if (option1Label) option1Label.text = label1;
        if (option2Label) option2Label.text = label2;

        option1Btn.onClick.AddListener(() => { action1?.Invoke(); Hide(); });
        option2Btn.onClick.AddListener(() => { action2?.Invoke(); Hide(); });

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
            out Vector2 local
        );
        rt.anchoredPosition = local;

        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
    public void Clear()
    {
        option1Btn.onClick.RemoveAllListeners();
        option2Btn.onClick.RemoveAllListeners();
    }
}
