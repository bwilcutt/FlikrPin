// =============================================================================
// File:        StickerTint.shader
// Author:      Bryan Wilcutt
// Date:        2026-06-05
// Description: Unlit shader that uses a texture's alpha channel for transparency
//              but replaces the RGB with a solid tint color. Used to show a
//              solid-color silhouette of a sticker when it is selected.
//              Assign _MainTex (the sticker texture) and _TintColor.
// =============================================================================
Shader "FlikrPin/StickerTint"
{
    Properties
    {
        _MainTex   ("Texture",    2D)    = "white" {}
        _TintColor ("Tint Color", Color) = (0, 1, 1, 1)
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
            fixed4    _TintColor;

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
                // Use texture alpha, replace RGB with tint color
                return fixed4(_TintColor.rgb, tex.a * _TintColor.a);
            }
            ENDCG
        }
    }
}
