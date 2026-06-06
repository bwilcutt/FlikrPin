// =============================================================================
// File:        SelectionOutline.shader
// Author:      Bryan Wilcutt
// Date:        2026-06-05
// Description: Two-pass outline shader. First pass renders the object normally
//              (transparent — so only the outline shows). Second pass renders
//              a scaled-up version with a solid color and inverted normals,
//              creating a visible outline around the original mesh.
//              Apply to a duplicate of the tag mesh parented to the tag.
// =============================================================================
Shader "FlikrPin/SelectionOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 1, 1, 1)
        _OutlineWidth ("Outline Width", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }

        // Pass 1 — invisible interior (just clears depth so outline shows cleanly)
        Pass
        {
            ZWrite On
            ColorMask 0
        }

        // Pass 2 — scaled-up inverted-normal solid color = outline
        Pass
        {
            ZWrite Off
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float  _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                // Push vertices out along normals in clip space
                float3 norm   = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 offset = TransformViewToProjection(norm.xy);
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.pos.xy     += offset * o.pos.z * _OutlineWidth;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
