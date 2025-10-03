using UnityEngine;

public class DogCleanliness : MonoBehaviour
{
    [Header("Visuals")]
    public SpriteRenderer dirtyOverlay;     // stains layer on top of dog
    public ParticleSystem rinseBurst;       // like slide #2 lines (optional)
    public GameObject sparkles;             // stars object (slide #3), disabled at start

    int total;
    int popped;

    void Start()
    {
        if (sparkles) sparkles.SetActive(false);
        SetProgress(0f);
    }

    public void SetTotal(int t) { total = Mathf.Max(1, t); }

    public void ReportPopped(int poppedCount)
    {
        popped = poppedCount;
        float t = Mathf.Clamp01((float)popped / total);
        SetProgress(t);

        if (rinseBurst) rinseBurst.Play();
    }

    void SetProgress(float t)
    {
        if (dirtyOverlay)
        {
            var c = dirtyOverlay.color;
            c.a = Mathf.Lerp(1f, 0f, t);   // fades dirt away
            dirtyOverlay.color = c;
        }
    }

    public void AllClean()
    {
        if (sparkles) sparkles.SetActive(true);
    }
}
