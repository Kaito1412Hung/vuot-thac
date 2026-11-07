Shader "VuotThac/ScenicFlow"
{
    Properties
    {
        _BaseColor("Water tint", Color) = (0.25,0.62,0.68,0.8)
        _Speed("Flow speed", Float) = 0.9
        _Foam("Foam", Range(0,1)) = 0.7
        _MouthFade("River mouth blend", Range(0,1)) = 0
        _Cascade("Cascade turbulence", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Speed, _Foam, _MouthFade, _Cascade;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; float3 world:TEXCOORD2; float3 normal:TEXCOORD3; };
            V Vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.fog=ComputeFogFactor(o.positionCS.z); o.world=TransformObjectToWorld(i.positionOS.xyz); o.normal=TransformObjectToWorldNormal(i.normalOS); return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(V i):SV_Target
            {
                float t=_Time.y*_Speed;
                float3 baseNormal=normalize(i.normal);
                float slope=1-abs(baseNormal.y);
                float2 p=lerp(i.world.xz*0.8,float2(i.uv.x*6,i.uv.y*5),slope);
                float n=Noise(p+float2(t*0.15,-t));
                float fine=Noise(p*3.8+float2(-t*0.3,-t*1.8));
                float foam=smoothstep(0.65,0.94,n*0.65+fine*0.35)*_Foam;
                foam=max(foam,smoothstep(0.72,0.94,fine)*slope*_Foam*0.65);
                float streak=Noise(float2(i.uv.x*26,i.uv.y*3-t*1.7));
                foam=max(foam,smoothstep(0.54,0.85,streak)*_Cascade*(0.25+0.5*slope));
                float edge=smoothstep(0,0.06,i.uv.x)*smoothstep(0,0.06,1-i.uv.x);
                float3 tangent=normalize(cross(baseNormal,abs(baseNormal.y)>0.9?float3(0,0,1):float3(0,1,0)));
                float3 bitangent=cross(baseNormal,tangent);
                float rippleA=sin(p.x*9+p.y*3+t*2)*0.045+sin(p.x*19-p.y*7-t*3)*0.02;
                float rippleB=cos(p.y*11+p.x*2-t*2.5)*0.045;
                float3 normal=normalize(baseNormal+tangent*rippleA+bitangent*rippleB);
                float3 view=normalize(GetWorldSpaceViewDir(i.world));
                normal=faceforward(normal,-view,normal);
                Light sun=GetMainLight();
                float fresnel=pow(1-saturate(dot(normal,view)),4);
                float shine=pow(saturate(dot(normal,normalize(view+sun.direction))),140);
                half3 color=_BaseColor.rgb*(0.88+0.16*n);
                color=lerp(color,half3(0.47,0.68,0.82),fresnel*0.45);
                color+=sun.color*shine*0.5;
                color=lerp(color,half3(0.91,0.97,0.99),foam);
                float mouth=1-_MouthFade*smoothstep(7.88,8.0,i.uv.y);
                return half4(MixFog(color,i.fog),_BaseColor.a*edge*mouth);
            }
            ENDHLSL
        }
    }
}
