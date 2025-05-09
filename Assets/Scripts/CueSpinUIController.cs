using UnityEngine;
using UnityEngine.UIElements;

public class CueSpinUIController : MonoBehaviour
{
    public RectTransform spinCircle;
    public RectTransform spinDot;
    public Vector2 currentSpin = Vector2.zero;

    public void SetSpin(Vector2 offset)
    {
        currentSpin = Vector2.ClampMagnitude(offset, 1f);
        UpdateSpinDotPosition();
    }

    private void UpdateSpinDotPosition()
    {
        float maxRadius = (spinCircle.rect.width * 0.5f) - (spinDot.rect.width * 0.5f);
        Vector2 pos = currentSpin * maxRadius;
        spinDot.anchoredPosition = pos;
    }
}
