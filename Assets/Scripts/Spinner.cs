using UnityEngine;

public class Spinner : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 90f;

    private void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }
}
