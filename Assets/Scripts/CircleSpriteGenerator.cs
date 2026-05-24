using UnityEngine;
using UnityEngine.UI;

public class CircleSpriteGenerator : MonoBehaviour
{
    public static Sprite CreateCircleSprite(int resolution = 128, bool filled = true, int borderThickness = 8)
    {
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
        Color[] pixels = new Color[resolution * resolution];

        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
        float outerRadius = resolution / 2f - 2f;
        float innerRadius = outerRadius - borderThickness;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);

                if (filled)
                {
                    // Filled circle
                    pixels[y * resolution + x] = dist <= outerRadius ? Color.white : Color.clear;
                }
                else
                {
                    // Ring only
                    pixels[y * resolution + x] = (dist <= outerRadius && dist >= innerRadius) ? Color.white : Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }
}
