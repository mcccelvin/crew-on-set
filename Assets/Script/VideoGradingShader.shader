Shader "UI/VideoGrading"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Brightness ("Brightness", Float) = 1.0
        _Contrast ("Contrast", Float) = 1.0
        _Saturation ("Saturation", Float) = 1.0
        _Tint ("Tint Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float _Brightness, _Contrast, _Saturation;
            fixed4 _Tint;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv) * _Tint;
                float3 gradedColor = max(col.rgb, 0.0);

                gradedColor *= _Brightness;
                gradedColor = ((gradedColor - 0.5) * _Contrast) + 0.5;

                float luminance = dot(gradedColor, float3(0.2126, 0.7152, 0.0722));
                gradedColor = lerp(float3(luminance, luminance, luminance), gradedColor, _Saturation);

                // Preserve color relationships when a strong grade pushes a channel over white.
                // This produces a softer commercial highlight instead of clipping red products flat.
                float brightestChannel = max(gradedColor.r, max(gradedColor.g, gradedColor.b));
                if (brightestChannel > 1.0) gradedColor /= brightestChannel;

                col.rgb = saturate(gradedColor);
                return col;
            }
            ENDCG
        }
    }
}
