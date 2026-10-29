using UnityEngine;

public class BollboardFollow : MonoBehaviour
{
    private void Update()
    {
        Quaternion rotation = transform.rotation = Camera.main.transform.rotation;
        transform.LookAt(transform.position + rotation * Vector3.forward, rotation * Vector3.up);
    }
}
