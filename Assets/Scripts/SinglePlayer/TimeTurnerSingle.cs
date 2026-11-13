using UnityEngine;
using System.Collections;

// Flips the sand clock over whenever the turn changes. Runs on every peer.
public class TimeTurnerSingle : MonoBehaviour
{
    [SerializeField] private float flipDuration = 0.6f;
    [SerializeField] private Vector3 flipAxis = Vector3.right;

    private GameController ganc;
    private bool flipping = false;

    private void Start()
    {
        // The asset's own animation just spins the clock forever, so turn it off
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false;
        ganc = FindFirstObjectByType<GameController>();
        ganc.currentPlayer.OnValueChanged += OnTurnChanged;
    }

    private void OnDestroy()
    {
        if (ganc != null) ganc.currentPlayer.OnValueChanged -= OnTurnChanged;
    }

    private void OnTurnChanged(ulong next)
    {
        TriggerAnimation();
    }

    [ContextMenu("Flip")] // Right-click the component in the Inspector during Play to test
    public void TriggerAnimation() {
        if (flipping) return; // Already flipping
        StartCoroutine(Flip());
    }

    private IEnumerator Flip()
    {
        flipping = true;
        Quaternion start = transform.localRotation;

        for (float t = 0f; t < flipDuration; t += Time.deltaTime)
        {
            float angle = 180f * Mathf.SmoothStep(0f, 1f, t / flipDuration);
            transform.localRotation = start * Quaternion.AngleAxis(angle, flipAxis);
            yield return null;
        }

        transform.localRotation = start * Quaternion.AngleAxis(180f, flipAxis);
        flipping = false;
    }
}
