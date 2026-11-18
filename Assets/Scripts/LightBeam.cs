using UnityEngine;
using UnityEngine.InputSystem;

public class LightBeam : MonoBehaviour
{
    [Tooltip("Degrees per second")]
    [SerializeField] float rotateSpeed = 45f;
    InputAction moveAction;
    Light spotLight;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Player/Move");
        spotLight = GetComponentInChildren<Light>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        transform.rotation *= Quaternion.Euler(Vector3.up * rotateSpeed * moveInput.x * Time.deltaTime);

        LightShips();
    }

    // Applies the new cone shape to every spotlight under this beam
    public void SetSpotAngles(float innerAngle, float outerAngle)
    {
        foreach (Light light in GetComponentsInChildren<Light>())
        {
            if (light.type != LightType.Spot)
                continue;
            light.spotAngle = outerAngle;
            light.innerSpotAngle = Mathf.Min(innerAngle, outerAngle);
        }
    }

    // A ship is lit when it's inside the spotlight's cone and range
    void LightShips()
    {
        if (spotLight == null)
            return;

        Vector3 origin = spotLight.transform.position;
        Vector3 forward = spotLight.transform.forward;
        float halfAngle = spotLight.spotAngle * 0.5f;

        foreach (Ship ship in Ship.All)
        {
            Vector3 toShip = ship.transform.position - origin;
            if (toShip.magnitude < spotLight.range && Vector3.Angle(forward, toShip) < halfAngle)
                ship.SetLit();
        }
    }
}
