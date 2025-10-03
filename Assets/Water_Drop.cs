using UnityEngine;

public class WaterDrop : MonoBehaviour
{
    [SerializeField] float life = 4f;
    float groundY = float.NegativeInfinity;
    bool useGround = false;

    public void Init(float groundY) { this.groundY = groundY; useGround = true; }

    void Start() => Destroy(gameObject, life);

    void Update()
    {
        if (useGround && transform.position.y <= groundY)
            Destroy(gameObject);
    }
}
