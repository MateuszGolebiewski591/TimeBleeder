using UnityEngine;

// Visual side of the angel, runs on every peer. The server decides which tile the angel
// is on (see AngelPathfinder / GameAndNetworkController); this just slides and turns
// the angel towards whatever position it is given.
public class AngelMovement : MonoBehaviour
{
    public float smoothMoveSpeed = 50f; // Speed at which the angel moves (smoothly)
    public float smoothRotationSpeed = 3600f; // Rotation speed in degrees per second

    private Vector3 movementTarget;
    private bool isMoving;
    private Quaternion rotationTarget;
    private bool isRotating;

    // snap = jump straight there (e.g. the first placement), otherwise move smoothly
    public void SetTarget(Vector3 worldPosition, bool snap)
    {
        if (snap)
        {
            transform.position = worldPosition;
            isMoving = false;
            return;
        }

        // Face the direction of travel
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            rotationTarget = Quaternion.LookRotation(direction, Vector3.up);
            isRotating = true;
        }

        movementTarget = worldPosition;
        isMoving = true;
    }

    // Smoothed rotation and movement
    private void Update()
    {
        if (isRotating)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                rotationTarget,
                smoothRotationSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, rotationTarget) < 0.1f)
            {
                transform.rotation = rotationTarget;
                isRotating = false;
            }
        }

        if (!isMoving)
            return;

        transform.position = Vector3.MoveTowards(transform.position, movementTarget, smoothMoveSpeed * Time.deltaTime);
        if ((transform.position - movementTarget).sqrMagnitude < 0.0001f)
        {
            transform.position = movementTarget;
            isMoving = false;
        }
    }
}
