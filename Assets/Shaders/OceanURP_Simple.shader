Shader "Custom/OceanURP_Simple"
{
    Properties
    {
        _BaseColor ("Shallow Color", Color) = (0.0, 0.35, 0.6, 0.6)
        _DeepColor ("Deep Color", Color) = (0.0, 0.1, 0.25, 0.8)
        _FresnelColor ("Fresnel Color", Color) = (0.6,0.9,1,1)

        _WaveScale ("Wave Scale", Float) = 0.05
        _WaveSpeed ("Wave Speed", Float) = 0.5
        _WaveHeight ("Wave Height", Float) = 0.5
        _Choppy ("Choppy", Float) = 2.0

        _FresnelPower ("Fresnel Power", Float) = 3.0

        _Opacity ("Opacity", Range(0,1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            float4 _BaseColor;
            float4 _DeepColor;
            float4 _FresnelColor;

            float _WaveScale;
            float _WaveSpeed;
            float _WaveHeight;
            float _Choppy;

            float _FresnelPower;
            float _Opacity;

            // ----------- 简化噪声 -----------
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(hash(i + float2(0,0)), hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y
                ) * 2 - 1;
            }

            float sea_octave(float2 uv, float choppy)
            {
                uv += noise(uv);
                float2 wv = 1.0 - abs(sin(uv));
                float2 swv = abs(cos(uv));
                wv = lerp(wv, swv, wv);
                return pow(1.0 - pow(wv.x * wv.y, 0.65), choppy);
            }

            float getWave(float2 uv)
            {
                float freq = 1.0;
                float amp = 1.0;
                float height = 0.0;

                for (int i = 0; i < 3; i++)
                {
                    height += sea_octave(uv * freq + _Time.y * _WaveSpeed, _Choppy) * amp;
                    freq *= 2.0;
                    amp *= 0.3;
                }

                return height * _WaveHeight;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);

                o.worldPos = worldPos;
                o.positionCS = TransformWorldToHClip(worldPos);

                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.worldPos.xz * _WaveScale;

                // 波动高度
                float h = getWave(uv);

                // 法线近似
                float dx = ddx(h);
                float dz = ddy(h);
                float3 normal = normalize(float3(-dx, 1.0, -dz));

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(normal, i.viewDir)), _FresnelPower);

                // 颜色混合
                float depthFactor = saturate(h);
                float3 waterColor = lerp(_DeepColor.rgb, _BaseColor.rgb, depthFactor);

                float3 finalColor = lerp(waterColor, _FresnelColor.rgb, fresnel);

                return float4(finalColor, _Opacity);
            }

            ENDHLSL
        }
    }
}
