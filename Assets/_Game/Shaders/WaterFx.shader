// Hiệu ứng nước quanh thuyền: hạt bọt/giọt nước mềm, vòng gợn sóng và dải bọt viền thân thuyền.
// Không cần texture: hình dạng tính từ UV, bọt được làm lổn nhổn bằng nhiễu theo toạ độ thế giới.
Shader "VuotThac/WaterFx"
{
    Properties
    {
        [Enum(Dot,0,Ring,1,Band,2)] _Mode ("Kiểu", Float) = 0
        _Alpha ("Độ đậm", Range(0,2)) = 1
        _NoiseScale ("Độ lổn nhổn của bọt", Float) = 2.2
    }
    SubShader
    {
        // Vẽ sau mặt nước (Transparent = 3000) để nằm đè lên nước
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Mode, _Alpha, _NoiseScale;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float3 world:TEXCOORD1; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            V Vert(A i)
            {
                V o; o.world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world); o.uv = i.uv; o.color = i.color; return o;
            }
            half4 Frag(V i):SV_Target
            {
                float a;
                if (_Mode < 0.5)
                {
                    float r = length(i.uv * 2 - 1);
                    a = pow(saturate(1 - r * r), 2);
                }
                else if (_Mode < 1.5)
                {
                    float r = length(i.uv * 2 - 1);
                    a = saturate(1 - abs(r - 0.78) / 0.16) * saturate((1 - r) * 8);
                }
                else
                {
                    // uv.y = 0 sát thân thuyền, 1 ở mép ngoài dải bọt
                    float2 p = i.world.xz * _NoiseScale;
                    float t = _Time.y;
                    float n = Noise(p + float2(t * 0.35, -t * 0.27)) * 0.6 + Noise(p * 2.7 - float2(t * 0.6, t * 0.45)) * 0.4;
                    float edge = 1 - i.uv.y;
                    a = saturate(edge * edge * 1.4 - (1 - n) * 0.75 * (1 - edge * 0.6));
                }
                return half4(i.color.rgb, saturate(i.color.a * a * _Alpha));
            }
            ENDHLSL
        }
    }
}
