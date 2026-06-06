// =============================================================================
// File:        StickerUnlit.shader
// Author:      Bryan Wilcutt
// Date:        2026-06-06
// Description: Unlit transparent shader for sticker tags. Supports _Color
//              property for brightness animation by TagSelectionManager pulse.
//              Preserves texture alpha for transparency. Renders both sides.
// =============================================================================
Shader "FlikrPin/StickerUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D)    = "white" {}
        _Color   ("Color",   Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4    _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                return tex * _Color;
            }
            ENDCG
        }
    }
}
