Shader "CrewOnSet/ProductionGuide"
{
    Properties
    {
        _MainTex("Filmstrip", 2D) = "white" {}
        _Flow("Direction cue speed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float _Flow;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            Output vert(Input v)
            {
                Output o; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.uv.x-=_Time.y*_Flow;
                o.color=v.color; return o;
            }
            fixed4 frag(Output i):SV_Target { return tex2D(_MainTex,i.uv)*i.color; }
            ENDCG
        }
    }
}
