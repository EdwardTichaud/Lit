using UnityEngine;

public class Destroy : MonoBehaviour
{
    public float delay = 2f;

    void Start()
    {
        Destroy(gameObject, delay);
    }

}
