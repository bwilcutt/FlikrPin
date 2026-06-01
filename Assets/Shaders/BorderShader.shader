Shader "Custom/BorderShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BorderColor ("Border Color", Color) = (0, 0, 0, 1)
        _BorderThickness ("Border Thickness", Range(0.0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BorderColor;
            float _BorderThickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Check if we're in the border region
                if (i.uv.x < _BorderThickness || 
                    i.uv.x > 1.0 - _BorderThickness ||
                    i.uv.y < _BorderThickness || 
                    i.uv.y > 1.0 - _BorderThickness)
                {
                    return _BorderColor;
                }

                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}