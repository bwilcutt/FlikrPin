using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws all sidebar icons procedurally using Unity's UI mesh drawing.
/// No sprite files required. Attach one instance per icon GameObject,
/// set the IconType in the Inspector, and it draws itself.
///
/// Attach to: Icon_Gear, Icon_Person1-4, Icon_Compass needle bg
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ProceduralIcon : Graphic
{
    public enum IconType
    {
        Gear,
        PersonOutline,
        Compass
    }

    [Header("Icon")]
    public IconType iconType = IconType.PersonOutline;

    [Header("Style")]
    public Color iconColor = new Color(1f, 1f, 1f, 0.9f);
    [Range(1f, 8f)]
    public float strokeWidth = 2.5f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        color = iconColor;

        Rect r = GetPixelAdjustedRect();
        Vector2 center = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
        float size = Mathf.Min(r.width, r.height);

        switch (iconType)
        {
            case IconType.Gear:         DrawGear(vh, center, size);         break;
            case IconType.PersonOutline:DrawPerson(vh, center, size);       break;
            case IconType.Compass:      DrawCompassFace(vh, center, size);  break;
        }
    }

    // ── Gear ─────────────────────────────────────────────────────────────

    void DrawGear(VertexHelper vh, Vector2 center, float size)
    {
        float outerR  = size * 0.42f;
        float innerR  = size * 0.26f;
        float holeR   = size * 0.14f;
        int   teeth   = 8;
        float toothW  = 0.18f; // radians half-width per tooth

        // Build gear outline as a polygon
        int segments = teeth * 4; // 4 points per tooth: rise, top, top, fall
        Vector2[] pts = new Vector2[segments];

        for (int i = 0; i < teeth; i++)
        {
            float baseAngle = (i / (float)teeth) * Mathf.PI * 2f;
            float next      = ((i + 1f) / teeth) * Mathf.PI * 2f;
            float mid       = (baseAngle + next) * 0.5f;

            pts[i * 4 + 0] = PolarToCart(center, innerR, baseAngle + toothW);
            pts[i * 4 + 1] = PolarToCart(center, outerR, mid - toothW);
            pts[i * 4 + 2] = PolarToCart(center, outerR, mid + toothW);
            pts[i * 4 + 3] = PolarToCart(center, innerR, next - toothW);
        }

        FillPolygon(vh, pts, color);

        // Punch centre hole (draw a white/transparent disc — use background color)
        // Since we can't subtract, we overdraw with a dark circle
        // Instead draw hole as a filled circle with the panel's background color
        Color bg = new Color(0f, 0f, 0f, 0.45f);
        DrawFilledCircle(vh, center, holeR, 20, bg);
    }

    // ── Person Outline ───────────────────────────────────────────────────

    void DrawPerson(VertexHelper vh, Vector2 center, float size)
    {
        float s = strokeWidth;

        // Head circle
        float headR  = size * 0.18f;
        float headCY = center.y + size * 0.22f;
        DrawRing(vh, new Vector2(center.x, headCY), headR, s, 24);

        // Body — rounded trapezoid: shoulders narrow to waist
        float bodyTop    = headCY - headR - size * 0.04f;
        float bodyBottom = center.y - size * 0.28f;
        float shoulderW  = size * 0.28f;
        float waistW     = size * 0.18f;

        // Left side
        DrawLine(vh,
            new Vector2(center.x - shoulderW, bodyTop),
            new Vector2(center.x - waistW,    bodyBottom), s);
        // Right side
        DrawLine(vh,
            new Vector2(center.x + shoulderW, bodyTop),
            new Vector2(center.x + waistW,    bodyBottom), s);
        // Bottom
        DrawLine(vh,
            new Vector2(center.x - waistW, bodyBottom),
            new Vector2(center.x + waistW, bodyBottom), s);
        // Top / shoulders
        DrawLine(vh,
            new Vector2(center.x - shoulderW, bodyTop),
            new Vector2(center.x + shoulderW, bodyTop), s);
    }

    // ── Compass Face ─────────────────────────────────────────────────────

    void DrawCompassFace(VertexHelper vh, Vector2 center, float size)
    {
        float outerR = size * 0.44f;
        float s      = strokeWidth;

        // Outer ring
        DrawRing(vh, center, outerR, s, 48);

        // Cardinal tick marks
        float tickOuter = outerR - s;
        float tickInner = outerR - size * 0.12f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector2 outer = PolarToCart(center, tickOuter, angle);
            Vector2 inner = PolarToCart(center, tickInner, angle);
            DrawLine(vh, outer, inner, i % 2 == 0 ? s * 1.5f : s);
        }

        // Centre dot
        DrawFilledCircle(vh, center, s * 1.5f, 12, color);
    }

    // ── Primitive helpers ────────────────────────────────────────────────

    Vector2 PolarToCart(Vector2 origin, float r, float angle)
        => new Vector2(origin.x + r * Mathf.Cos(angle),
                       origin.y + r * Mathf.Sin(angle));

    void DrawLine(VertexHelper vh, Vector2 a, Vector2 b, float width)
    {
        Vector2 dir  = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);

        int idx = vh.currentVertCount;
        AddVert(vh, a - perp);
        AddVert(vh, a + perp);
        AddVert(vh, b + perp);
        AddVert(vh, b - perp);
        vh.AddTriangle(idx,     idx + 1, idx + 2);
        vh.AddTriangle(idx,     idx + 2, idx + 3);
    }

    void DrawRing(VertexHelper vh, Vector2 center, float radius, float width, int segs)
    {
        float innerR = radius - width;
        for (int i = 0; i < segs; i++)
        {
            float a0 = (i      / (float)segs) * Mathf.PI * 2f;
            float a1 = ((i+1f) / segs)        * Mathf.PI * 2f;

            int idx = vh.currentVertCount;
            AddVert(vh, PolarToCart(center, innerR,  a0));
            AddVert(vh, PolarToCart(center, radius,  a0));
            AddVert(vh, PolarToCart(center, radius,  a1));
            AddVert(vh, PolarToCart(center, innerR,  a1));
            vh.AddTriangle(idx, idx+1, idx+2);
            vh.AddTriangle(idx, idx+2, idx+3);
        }
    }

    void DrawFilledCircle(VertexHelper vh, Vector2 center, float radius, int segs, Color c)
    {
        int startIdx = vh.currentVertCount;
        AddVert(vh, center, c);
        for (int i = 0; i <= segs; i++)
        {
            float angle = (i / (float)segs) * Mathf.PI * 2f;
            AddVert(vh, PolarToCart(center, radius, angle), c);
        }
        for (int i = 0; i < segs; i++)
            vh.AddTriangle(startIdx, startIdx + i + 1, startIdx + i + 2);
    }

    void FillPolygon(VertexHelper vh, Vector2[] pts, Color c)
    {
        int startIdx = vh.currentVertCount;
        // Fan triangulation from first point
        foreach (var p in pts) AddVert(vh, p, c);
        for (int i = 1; i < pts.Length - 1; i++)
            vh.AddTriangle(startIdx, startIdx + i, startIdx + i + 1);
    }

    void FillTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        int idx = vh.currentVertCount;
        AddVert(vh, a, col);
        AddVert(vh, b, col);
        AddVert(vh, c, col);
        vh.AddTriangle(idx, idx+1, idx+2);
    }

    void AddVert(VertexHelper vh, Vector2 pos)
        => AddVert(vh, pos, color);

    void AddVert(VertexHelper vh, Vector2 pos, Color c)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = new Vector3(pos.x, pos.y, 0);
        v.color    = c;
        vh.AddVert(v);
    }
}
