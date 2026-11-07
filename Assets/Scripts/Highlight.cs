using UnityEngine;

public class Highlight : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
        ApplyColours();
    }

    public void ApplyColours() //Colour properties
    {
        if (meshRenderer == null)
            return;

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat("_IsAllowed", 1f);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    public void TurnOn() {
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat("_IsAllowed", 1f);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    public void TurnOff() {
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat("_IsAllowed", 0f);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}

