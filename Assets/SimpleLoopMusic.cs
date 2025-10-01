using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SimpleLoopMusic : MonoBehaviour
{
    [Range(0f,1f)] public float volume = 0.25f;
    [Range(8000,96000)] public int sampleRate = 44100;
    [Range(5f,120f)] public float durationSeconds = 30f;

    void Start()
    {
        int samples = Mathf.CeilToInt(sampleRate * durationSeconds);
        var clip = AudioClip.Create("PlaceholderMusic", samples, 1, sampleRate, false);

        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float f1 = 200f + 40f * Mathf.Sin(t * 0.8f);
            float f2 = 300f + 25f * Mathf.Sin(t * 0.5f + 1.3f);
            float f3 = 260f + 30f * Mathf.Sin(t * 0.3f + 0.6f);
            float s = (Mathf.Sin(2*Mathf.PI*f1*t)
                     + 0.5f*Mathf.Sin(2*Mathf.PI*f2*t)
                     + 0.3f*Mathf.Sin(2*Mathf.PI*f3*t)) / 1.8f;
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.0f));
            float release = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t - (durationSeconds - 1f)) / 1f));
            data[i] = s * attack * release * volume;
        }

        clip.SetData(data, 0);

        var src = GetComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.Play();   // <-- make sure we actually start after setting the clip

    }
}
