Shader "VuotThac/BoatWaterOcclusion"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; };
            V Vert(A i){ V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o; }
            half4 Frag(V i):SV_Target{return 0;}
            ENDHLSL
        }
    }
}
