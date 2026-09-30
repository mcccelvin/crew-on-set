Shader "Crew On Set/Studio Surface"
{
    Properties
    {
        _Color ("Surface Color", Color) = (0.8,0.77,0.7,1)
        _Detail ("Texture Strength", Range(0,0.3)) = 0.12
        _Panels ("Acoustic Ceiling Panels", Range(0,1)) = 0
        _PanelSize ("Panel Size (Metres)", Float) = 0.6
        _Finish ("Finish: 0 Plaster, 1 Metal, 2 Grip, 3 Timber", Float) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200
        Pass
        {
        Tags { "LightMode"="UniversalForward" }
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile_fog
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "StudioMetalPanels.cginc"
        CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        float _Detail, _Panels, _PanelSize, _Finish, _Metallic, _Smoothness;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 worldPos : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };
        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
            OUT.positionCS = TransformWorldToHClip(OUT.worldPos);
            OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
            OUT.fog = ComputeFogFactor(OUT.positionCS.z);
            return OUT;
        }
        float hash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        float grain(float3 p)
        {
            float3 cell = floor(p), f = frac(p);
            f = f*f*(3-2*f);
            return lerp(lerp(lerp(hash(cell), hash(cell+float3(1,0,0)), f.x),
                             lerp(hash(cell+float3(0,1,0)), hash(cell+float3(1,1,0)), f.x), f.y),
                        lerp(lerp(hash(cell+float3(0,0,1)), hash(cell+float3(1,0,1)), f.x),
                             lerp(hash(cell+float3(0,1,1)), hash(cell+float3(1,1,1)), f.x), f.y), f.z);
        }
        half4 frag(Varyings IN) : SV_Target
        {
            float3 p = IN.worldPos;
            float fade = saturate(1 - length(fwidth(p * 55)));
            float surfaceDetail = (grain(p * 7)-0.5)*0.45 + (grain(p * 55)-0.5)*fade;
            if (_Finish > 0.5 && _Finish < 1.5)
                surfaceDetail = (grain(p * float3(2,180,2)) - 0.5) * saturate(1-length(fwidth(p*float3(2,180,2))));
            if (_Finish > 1.5)
                surfaceDetail = (grain(p*90)-0.5)*saturate(1-length(fwidth(p*90))) + 0.3*sin((p.x+p.z)*100)*sin((p.x-p.z)*100)*saturate(1-length(fwidth(p*100)));
            float2 uv = p.xz / max(0.1, _PanelSize);
            float2 edge = min(frac(uv), 1-frac(uv));
            float aa = max(max(fwidth(uv.x), fwidth(uv.y)), 0.001);
            float seam = 1-smoothstep(0.004, 0.004+aa, min(edge.x, edge.y));
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = _Color.rgb * (1 + surfaceDetail * _Detail) * (1 - seam * 0.3 * _Panels);
            if (_Finish > 2.5) surface.albedo = StudioTimber(p, IN.normalWS, _Color.rgb, _Detail);
            surface.metallic = _Metallic;
            surface.smoothness = saturate(_Smoothness + surfaceDetail * 0.12);
            if (_Finish > 0.5 && _Finish < 1.5)
            {
                float3 panelColor = surface.albedo;
                float panelMetal = surface.metallic, panelSmooth = surface.smoothness;
                StudioMetalPanels(p, IN.normalWS, panelColor, panelMetal, panelSmooth);
                surface.albedo = panelColor;
                surface.metallic = panelMetal;
                surface.smoothness = panelSmooth;
            }
            surface.occlusion = 1 - seam * 0.15 * _Panels;
            surface.alpha = 1;
            surface.normalTS = half3(0,0,1);
            InputData data = (InputData)0;
            data.positionWS = p;
            data.normalWS = NormalizeNormalPerPixel(IN.normalWS);
            data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
            data.shadowCoord = TransformWorldToShadowCoord(p);
            data.bakedGI = SampleSH(data.normalWS);
            data.vertexLighting = VertexLighting(p, data.normalWS);
            data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
            data.shadowMask = half4(1,1,1,1);
            half4 color = UniversalFragmentPBR(data, surface);
            color.rgb = MixFog(color.rgb, IN.fog);
            return color;
        }
        ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #include "StudioMetalPanels.cginc"
        float4 _Color;
        float _Detail, _Panels, _PanelSize, _Finish, _Metallic, _Smoothness;
        struct Input { float3 worldPos; float3 worldNormal; };
        float surfaceHash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        float surfaceNoise(float3 p)
        {
            float3 c = floor(p), f = frac(p);
            f = f*f*(3-2*f);
            return lerp(lerp(lerp(surfaceHash(c), surfaceHash(c+float3(1,0,0)), f.x),
                lerp(surfaceHash(c+float3(0,1,0)), surfaceHash(c+float3(1,1,0)), f.x), f.y),
                lerp(lerp(surfaceHash(c+float3(0,0,1)), surfaceHash(c+float3(1,0,1)), f.x),
                lerp(surfaceHash(c+float3(0,1,1)), surfaceHash(c+float3(1,1,1)), f.x), f.y), f.z);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.worldPos;
            float detail = (surfaceNoise(p*7)-0.5)*0.45 + (surfaceNoise(p*55)-0.5)*saturate(1-length(fwidth(p*55)));
            if (_Finish > 0.5 && _Finish < 1.5)
                detail = (surfaceNoise(p*float3(2,180,2))-0.5)*saturate(1-length(fwidth(p*float3(2,180,2))));
            if (_Finish > 1.5)
                detail = (surfaceNoise(p*90)-0.5)*saturate(1-length(fwidth(p*90))) + 0.3*sin((p.x+p.z)*100)*sin((p.x-p.z)*100)*saturate(1-length(fwidth(p*100)));
            float2 uv = p.xz / max(0.1, _PanelSize);
            float2 edge = min(frac(uv), 1-frac(uv));
            float aa = max(max(fwidth(uv.x),fwidth(uv.y)),0.001);
            float seam = 1-smoothstep(0.004,0.004+aa,min(edge.x,edge.y));
            o.Albedo = _Color.rgb * (1+detail*_Detail) * (1-seam*0.3*_Panels);
            if (_Finish > 2.5) o.Albedo = StudioTimber(p, IN.worldNormal, _Color.rgb, _Detail);
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Smoothness + detail*0.12);
            if (_Finish > 0.5 && _Finish < 1.5)
            {
                float3 panelColor = o.Albedo;
                float panelMetal = o.Metallic, panelSmooth = o.Smoothness;
                StudioMetalPanels(p, IN.worldNormal, panelColor, panelMetal, panelSmooth);
                o.Albedo = panelColor;
                o.Metallic = panelMetal;
                o.Smoothness = panelSmooth;
            }
            o.Occlusion = 1-seam*0.15*_Panels;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
