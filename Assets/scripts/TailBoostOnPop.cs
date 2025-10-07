using UnityEngine;

public class TailBoostOnPop : MonoBehaviour
{
    public TailWag tail;  // drag your TailPivot here

    void OnEnable(){ Bubble.OnAnyPopped += Handle; }
    void OnDisable(){ Bubble.OnAnyPopped -= Handle; }

    void Handle(Bubble b)
    {
        if (tail) tail.NotePop();   // ⬅️ extend the 2s boost window
    }
}
