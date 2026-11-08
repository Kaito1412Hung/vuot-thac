// Thuyền bóng ma: xanh trong suốt, viền sáng theo góc nhìn. Nằm trong Resources để luôn có trong bản build.
Shader "VuotThac/GhostBoat"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 viewWS:TEXCOORD1; };
            V Vert(A i)
            {
                V o;
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(world);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                float rim = pow(1 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), 2);
                return half4(lerp(float3(0.35, 0.8, 1), float3(0.85, 0.97, 1), rim), 0.16 + rim * 0.55);
            }
            ENDHLSL
        }
    }
}
