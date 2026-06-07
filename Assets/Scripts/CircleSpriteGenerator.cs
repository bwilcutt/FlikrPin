// =============================================================================
// File:        CircleSpriteGenerator.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Static utility that generates a circular Sprite at runtime from
//              a programmatically filled Texture2D. Supports both filled circle
//              and ring (outline-only) modes.
// =============================================================================

using UnityEngine;

public class CircleSpriteGenerator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Function:    CreateCircleSprite
    // Inputs:      resolution      — texture dimensions in pixels (default 128)
    //              filled          — true = filled disc, false = ring outline
    //              borderThickness — ring thickness in pixels (used when filled=false)
    // Outputs:     Sprite — generated circle sprite centered at (0.5, 0.5)
    // Description: Creates a square ARGB32 texture, paints white pixels inside
    //              the circle (or ring) boundary, and wraps it in a Sprite.
    // -------------------------------------------------------------------------
    public static Sprite CreateCircleSprite(int resolution = 128, bool filled = true, int borderThickness = 8)
    {
        // Allocate texture and pixel buffer
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
        Color[]   pixels  = new Color[resolution * resolution];

        // Circle center in pixel space
        Vector2 center = new Vector2(resolution / 2f, resolution / 2f);

        // Leave a 2px margin so the edge doesn't get clipped by the texture border
        float outerRadius = resolution / 2f - 2f;

        // Inner radius determines ring thickness when filled=false
        float innerRadius = outerRadius - borderThickness;

        // Walk every pixel and decide whether it falls inside the circle shape
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // Distance from this pixel to the circle center
                float dist = Vector2.Distance(new Vector2(x, y), center);

                if (filled)
                {
                    // Filled disc: white inside outer radius, transparent outside
                    pixels[y * resolution + x] = dist <= outerRadius ? Color.white : Color.clear;
                }
                else
                {
                    // Ring: white only between inner and outer radius
                    pixels[y * resolution + x] = (dist <= outerRadius && dist >= innerRadius)
                        ? Color.white
                        : Color.clear;
                }
            }
        }

        // Push pixel data to the texture and upload to GPU
        texture.SetPixels(pixels);
        texture.Apply();

        // Wrap the texture in a Sprite, pivot at center
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }
}
