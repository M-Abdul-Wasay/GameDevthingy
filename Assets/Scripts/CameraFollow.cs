using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 1.5f;
    public float distance = 8f;
    public float smoothSpeed = 12f;

    [Header("Rotate")]
    public float mouseSensitivity = 3f;
    public float minPitch = -10f;
    public float maxPitch = 70f;
    private float yaw;
    private float pitch = 25f;

    // Read by ShadowHeroController so WASD follows the mouse direction
    public float Yaw { get { return yaw; } }

    [Header("Zoom")]
    public float zoomSpeed = 4f;
    public float minDistance = 2f;
    public float maxDistance = 15f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Esc frees the mouse, click in the Game view to lock it again
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (target != null)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * height;
            transform.position = focus - rot * Vector3.forward * distance;
            transform.LookAt(focus);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minDistance, maxDistance);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + Vector3.up * height;
        Vector3 desired = focus - rot * Vector3.forward * distance;

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        transform.LookAt(focus);
    }
}
