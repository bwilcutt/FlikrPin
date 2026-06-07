// =============================================================================
// File:        CalloutBubble.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Procedurally generates a rounded-rectangle mesh with a downward
//              tail for use as a speech/callout bubble in world space. Rebuilds
//              the mesh in LateUpdate whenever the content text bounds change.
//              Expects a child GameObject named "content" with a TextMeshPro
//              component for the bubble's text content.
// =============================================================================

using UnityEngine;
using TMPro;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CalloutBubble : MonoBehaviour
{
    [Header("Bubble Settings")]
    public float cornerRadius   = 0.15f;
    public int   cornerSegments = 8;
    public Color bubbleColor    = new Color(0.5f, 0.8f, 1f, 0.8f);
    public float padding        = 0.1f;

    [Header("Tail Settings")]
    public float tailWidth  = 0.1f;
    public float tailLength = 0.25f;

    private MeshFilter    meshFilter;
    private MeshRenderer  meshRenderer;
    private TextMeshPro   contentText;
    private float         lastWidth  = -1f;
    private float         lastHeight = -1f;

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Caches component references, configures the content TextMeshPro
    //              rect and rendering order, and assigns a transparency-capable
    //              material to the bubble mesh renderer.
    // -------------------------------------------------------------------------
    void Awake()
    {
        meshFilter   = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        Transform contentTransform = transform.Find("content");
        if (contentTransform != null)
        {
            contentText = contentTransform.GetComponent<TextMeshPro>();

            if (contentText != null)
            {
                contentText.alignment        = TextAlignmentOptions.Center;
                contentText.enableWordWrapping = true;
                contentText.overflowMode     = TextOverflowModes.Truncate;

                // Size rect to approximately 24 characters wide x 12 lines tall
                float charWidth  = contentText.fontSize * 0.06f;
                float rectWidth  = charWidth * 24f;
                float rectHeight = contentText.fontSize * 12f;
                contentText.rectTransform.sizeDelta = new Vector2(rectWidth, rectHeight);

                // Push text in front of the bubble mesh so it always draws on top
                contentText.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                contentText.renderer.sortingOrder   = 1;
            }
        }

        // Sprites/Default supports transparency and renders correctly in world space
        meshRenderer.material           = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material.color     = bubbleColor;
        meshRenderer.sortingOrder       = 0;
    }

    // -------------------------------------------------------------------------
    // Function:    LateUpdate
    // Inputs:      None
    // Outputs:     None
    // Description: Measures the current text bounds and regenerates the bubble
    //              mesh if the required size has changed. Runs in LateUpdate so
    //              TMP has already laid out text for this frame.
    // -------------------------------------------------------------------------
    void LateUpdate()
    {
        if (contentText == null) return;

        contentText.ForceMeshUpdate();
        Bounds bounds = contentText.textBounds;
        if (bounds.size == Vector3.zero) return;

        float textScale       = contentText.transform.localScale.x;
        float minBottomPadding = tailLength;

        float w = contentText.rectTransform.sizeDelta.x * textScale + padding * 2f;
        float h = bounds.size.y * textScale + padding * 2f + minBottomPadding;

        w = Mathf.Max(w, 0.3f);
        h = Mathf.Max(h, 0.15f);

        if (Mathf.Abs(w - lastWidth) > 0.001f || Mathf.Abs(h - lastHeight) > 0.001f)
        {
            lastWidth  = w;
            lastHeight = h;
            GenerateMesh(w, h);
        }

        contentText.transform.localPosition = new Vector3(0f, 0f, -0.05f);
    }

    // -------------------------------------------------------------------------
    // Function:    GenerateMesh
    // Inputs:      width  — total bubble width in world units
    //              height — total bubble height in world units (excluding tail)
    // Outputs:     None
    // Description: Builds a rounded-rectangle mesh with four arc corners and a
    //              downward-pointing triangular tail. Uses fan triangulation from
    //              a center vertex. Assigns the result to the MeshFilter.
    // -------------------------------------------------------------------------
    public void GenerateMesh(float width, float height)
    {
        float halfW = width  / 2f;
        float halfH = height / 2f;
        float cr    = Mathf.Min(cornerRadius, halfW, halfH);

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris  = new System.Collections.Generic.List<int>();

        // Center vertex for fan triangulation
        verts.Add(Vector3.zero);

        // Arc centers for the four corners: top-right, top-left, bottom-left, bottom-right
        Vector2[] cornerCenters = new Vector2[]
        {
            new Vector2( halfW - cr,  halfH - cr),
            new Vector2(-halfW + cr,  halfH - cr),
            new Vector2(-halfW + cr, -halfH + cr),
            new Vector2( halfW - cr, -halfH + cr),
        };

        float[] startAngles = new float[] { 0f, 90f, 180f, 270f };

        for (int c = 0; c < 4; c++)
        {
            for (int s = 0; s <= cornerSegments; s++)
            {
                float angle = (startAngles[c] + (90f / cornerSegments) * s) * Mathf.Deg2Rad;
                verts.Add(new Vector3(
                    cornerCenters[c].x + Mathf.Cos(angle) * cr,
                    cornerCenters[c].y + Mathf.Sin(angle) * cr,
                    0f));
            }
        }

        // Tail — three verts forming a downward-pointing triangle at bubble bottom
        int tailStart = verts.Count;
        verts.Add(new Vector3(-tailWidth,  -halfH,              0f));
        verts.Add(new Vector3( tailWidth,  -halfH,              0f));
        verts.Add(new Vector3( 0f,         -halfH - tailLength, 0f));

        // Fan triangles for the rounded rectangle body
        int bodyVerts = (cornerSegments + 1) * 4;
        for (int i = 1; i <= bodyVerts; i++)
        {
            int next = (i % bodyVerts) + 1;
            tris.Add(0);
            tris.Add(i);
            tris.Add(next);
        }

        // Tail triangle
        tris.Add(tailStart);
        tris.Add(tailStart + 2);
        tris.Add(tailStart + 1);

        Mesh mesh      = new Mesh();
        mesh.vertices  = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }
}
