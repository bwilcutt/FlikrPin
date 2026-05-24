using UnityEngine;
using UnityEngine.UI;

public class ReticleController : MonoBehaviour
{
    [Header("Reticle Visuals")]
    public RectTransform dot;
    public RectTransform ring;

    public float dotSize = 10f;
    public float ringSize = 60f;
    public Color reticleColor = Color.white;

    [Header("Press Feedback")]
    public bool animateOnTap = true;
    public float tapScale = 1.4f;
    public float tapDuration = 0.15f;

    void Start()
    {
        try
        {
            if (dot != null)
            {
                Image dotImage = dot.GetComponent<Image>();
                if (dotImage != null)
                {
                    dotImage.sprite = CircleSpriteGenerator.CreateCircleSprite(64, true);
                    dotImage.color = reticleColor;
                }
                dot.sizeDelta = new Vector2(dotSize, dotSize);
            }

            if (ring != null)
            {
                Image ringImage = ring.GetComponent<Image>();
                if (ringImage != null)
                {
                    ringImage.sprite = CircleSpriteGenerator.CreateCircleSprite(128, false, 6);
                    ringImage.color = new Color(reticleColor.r, reticleColor.g, reticleColor.b, 0.5f);
                }
                ring.sizeDelta = new Vector2(ringSize, ringSize);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("ReticleController initialization error: " + e.Message);
        }
    }
}
