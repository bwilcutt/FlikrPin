using UnityEngine;
using TMPro;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CalloutBubble : MonoBehaviour
{
    [Header("Bubble Settings")]
    public float cornerRadius = 0.15f;
    public int cornerSegments = 8;
    public Color bubbleColor = new Color(0.5f, 0.8f, 1f, 0.8f);
    public float padding = 0.1f;

    [Header("Tail Settings")]
    public float tailWidth = 0.1f;
    public float tailLength = 0.25f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private TextMeshPro contentText;
    private float lastWidth = -1f;
    private float lastHeight = -1f;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        Transform contentTransform = transform.Find("content");
        if (contentTransform != null)
        {
            contentText = contentTransform.GetComponent<TextMeshPro>();

            if (contentText != null)
            {
                // Center the text alignment so it sits correctly inside the bubble
                contentText.alignment = TextAlignmentOptions.Center;
		// Lock the TMP rect to 24 chars wide x 12 lines tall
		contentText.enableWordWrapping = true;
		contentText.overflowMode = TextOverflowModes.Truncate;

		// Set rect width to 24 characters wide using monospace character width
		float charWidth = contentText.fontSize * 0.06f; // RobotoMono character aspect ratio
		float rectWidth = charWidth * 24f;
		float rectHeight = contentText.fontSize * 12f;
		contentText.rectTransform.sizeDelta = new Vector2(rectWidth, rectHeight);
		contentText.enableWordWrapping = true;
		contentText.overflowMode = TextOverflowModes.Truncate;
		
                // Push text in front of the bubble mesh so it's always drawn on top
                // Use a small negative Z since the camera looks down -Z in Unity
                contentText.transform.localPosition = new Vector3(0f, 0f, -0.05f);

                // Make sure TMP renders on top of the bubble mesh
                contentText.renderer.sortingOrder = 1;
            }
        }

        // Use a material that supports transparency and draws correctly in world space
        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material.color = bubbleColor;
        meshRenderer.sortingOrder = 0;
    }

	void LateUpdate()
	{
	    if (contentText == null) return;

	    contentText.ForceMeshUpdate();
	    Bounds bounds = contentText.textBounds;

	    if (bounds.size == Vector3.zero) return;

	    float textScale = contentText.transform.localScale.x;

	    // 3 line minimum padding below text
	    float lineHeight = contentText.fontSize * textScale;
	    float minBottomPadding = tailLength;

	    float w = contentText.rectTransform.sizeDelta.x * textScale + padding * 2f;
	    float h = bounds.size.y * textScale + padding * 2f + minBottomPadding;

	    w = Mathf.Max(w, 0.3f);
	    h = Mathf.Max(h, 0.15f);

	    if (Mathf.Abs(w - lastWidth) > 0.001f || Mathf.Abs(h - lastHeight) > 0.001f)
	    {
		lastWidth = w;
		lastHeight = h;
		GenerateMesh(w, h);
	    }

	    contentText.transform.localPosition = new Vector3(0f, 0f, -0.05f);
	}
	
    public void GenerateMesh(float width, float height)
    {
        float halfW = width / 2f;
        float halfH = height / 2f;
        float cr = Mathf.Min(cornerRadius, halfW, halfH);

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris  = new System.Collections.Generic.List<int>();

        // Center vertex for fan triangulation
        verts.Add(Vector3.zero);

        // Four rounded corners: top-right, top-left, bottom-left, bottom-right
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

        // Tail — three verts forming a downward-pointing triangle at the bubble bottom
        int tailStart = verts.Count;
        verts.Add(new Vector3(-tailWidth,  -halfH,               0f));
        verts.Add(new Vector3( tailWidth,  -halfH,               0f));
        verts.Add(new Vector3( 0f,         -halfH - tailLength,  0f));

        // Fan triangles for the rounded rectangle body
        int bodyVerts = (cornerSegments + 1) * 4;
        for (int i = 1; i <= bodyVerts; i++)
        {
            int next = (i % bodyVerts) + 1;
            tris.Add(0);
            tris.Add(i);
            tris.Add(next);
        }

        // Tail triangle (wound correctly for front-face)
        tris.Add(tailStart);
        tris.Add(tailStart + 2);
        tris.Add(tailStart + 1);

        Mesh mesh = new Mesh();
        mesh.vertices  = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }
}
