Shader "CrewOnSet/StudioLightHaze"
{
 Properties { _BeamColor("Beam color",Color)=(1,1,1,1) _Density("Haze density",Float)=.04 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Cull Front ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   float4 _BeamColor; float _Density;
   struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float4 screen:TEXCOORD1; };
   v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.screen=ComputeScreenPos(o.pos); return o; }
   fixed4 frag(v2f i):SV_Target
   {
    float3 ray=normalize(i.world-_WorldSpaceCameraPos);
    float3 origin=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
    float3 direction=mul(unity_WorldToObject,float4(ray,0)).xyz;
    float3 inverseDirection=1.0/(direction+1e-7);
    float3 t0=(-.5-origin)*inverseDirection, t1=(.5-origin)*inverseDirection;
    float3 nearT=min(t0,t1), farT=max(t0,t1);
    float start=max(0,max(nearT.x,max(nearT.y,nearT.z)));
    float finish=min(farT.x,min(farT.y,farT.z));
    float rawDepth=SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen));
    float sceneDepth=LinearEyeDepth(rawDepth);
    float viewZ=max(.001,-mul(UNITY_MATRIX_V,float4(ray,0)).z);
    finish=min(finish,sceneDepth/viewZ);
    if(finish<=start)discard;
    float stepSize=(finish-start)/24; float density=0;
    [unroll] for(int s=0;s<24;s++)
    {
     float t=start+(s+.5)*stepSize;
     float3 p=origin+direction*t;
     float z=p.z+.5;
     float edge=1-smoothstep(.55,1,length(p.xy)/max(.008,z*.5));
     float fade=smoothstep(0,.08,z)*(1-smoothstep(.6,1,z));
     float wisps=.88+.12*sin(p.x*23+p.y*17+z*37+_Time.y*.3);
     density+=edge*fade*wisps*stepSize;
    }
    return float4(_BeamColor.rgb, min(.23,1-exp(-density*_Density)));
   }
   ENDCG
  }
 }
}
