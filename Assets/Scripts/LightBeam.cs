using UnityEngine;
using UnityEngine.InputSystem;

public class LightBeam : MonoBehaviour
{
    [Tooltip("Degrees per second")]
    [SerializeField] float rotateSpeed = 45f;
    InputAction moveAction;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Player/Move");
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        transform.rotation *= Quaternion.Euler(Vector3.up * rotateSpeed * moveInput.x * Time.deltaTime);
    }
}
