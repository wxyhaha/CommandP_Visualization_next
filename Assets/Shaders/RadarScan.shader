Shader "Custom/RadarScan"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.25, 0.25, 1)
        _ConeAngle ("Cone Angle (total deg)", Range(1, 180)) = 60
        _MaxDistance ("Max Distance", Range(0.1, 1.0)) = 1.0
        _EdgeSoftness ("Edge Softness", Range(0.0, 0.2)) = 0.05

        _WaveDensity ("Wave Density", Range(1, 20)) = 8
        _WaveSpeed ("Wave Speed", Range(0.1, 10)) = 4
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.4

        _Intensity ("Intensity", Range(0, 2)) = 1.0
        _EmissionIntensity ("Emission Intensity", Float) = 2.5

        _BreathSpeed ("Breath Speed", Float) = 0.6
        _BreathStrength ("Breath Strength", Range(0, 0.5)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RadarScanForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #ifndef PI
            #define PI 3.14159265359
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _ConeAngle;
                float  _MaxDistance;
                float  _StartOffset;
                float  _EdgeSoftness;
                float  _WaveDensity;
                float  _WaveSpeed;
                float  _WaveStrength;
                float  _Intensity;
                float  _EmissionIntensity;
                float  _BreathSpeed;
                float  _BreathStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Use the generated cone mesh's local +Z as the distance axis.
                float distance = IN.positionOS.z - _StartOffset;
                if (distance < 0.0 || distance > _MaxDistance)
                {
                    discard;
                }

                float distance01 = saturate(distance / max(_MaxDistance, 0.0001));
                float radial = length(IN.positionOS.xy);

                float distanceLen = distance01 * _MaxDistance;

                // Soft cone edge
                float halfAngleRad = radians(_ConeAngle * 0.5);
                float maxWidth = distanceLen * tan(halfAngleRad);
                float edgeSoftness = max(_EdgeSoftness * maxWidth, 0.0001);
                float coneEdge = 1.0 - smoothstep(
                    max(0.0, maxWidth - edgeSoftness),
                    maxWidth + edgeSoftness,
                    radial);

                // Distance fade: brighter near the tip, softer toward the base.
                float distFade = 1.0 - distance01;

                // Animated wave along the cone length
                float wave = sin((distance01 * _WaveDensity - _Time.y * _WaveSpeed) * 2.0 * PI);
                wave = wave * 0.5 + 0.5; // remap to 0..1
                float energy = lerp(1.0, wave, _WaveStrength);

                // Breathing pulse
                float breath = lerp(
                    1.0 - _BreathStrength,
                    1.0 + _BreathStrength * 0.5,
                    0.5 + 0.5 * sin(_Time.y * max(_BreathSpeed, 0.001) * 2.0 * PI));

                // Composite alpha
                float alpha = coneEdge * distFade * energy * _Intensity * breath;
                alpha = max(alpha, 0.025 * _Intensity * breath * (1.0 - distance01 * 0.7));
                alpha = saturate(alpha);

                // Color: pure additive glow so the aircraft body is not darkened.
                float3 rgb = _Color.rgb * (0.15 + alpha * _EmissionIntensity);

                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
