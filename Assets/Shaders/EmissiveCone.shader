Shader "Unlit/EmissiveCone"
{
    Properties
    {
        _Color ("Color", Color) = (0.12, 0.9, 0.85, 0.22)
        _Length ("Length", Float) = 300
        _EmissionIntensity ("Emission Intensity", Float) = 2.2
        _ScanTime ("Scan Time", Float) = 0
        _Repeat ("Repeat", Float) = 14
        _Thickness ("Thickness", Range(0, 1)) = 0.22
        _Offset ("Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            float4 _Color;
            float _Length;
            float _EmissionIntensity;
            float _ScanTime;
            float _Repeat;
            float _Thickness;
            float _Offset;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float distance01 = saturate(i.localPos.z / max(0.0001, _Length));
                float phase = distance01 * max(_Repeat, 0.0001) - _ScanTime + _Offset;
                float wave = frac(phase);

                float pulse = 1.0 - smoothstep(
                    max(0.0, 1.0 - _Thickness),
                    1.0,
                    wave);

                float frontFade = 1.0 - smoothstep(0.88, 1.0, distance01);
                float depthFade = lerp(1.0, 0.35, distance01);
                float emission = pulse * frontFade * depthFade;

                float alpha = saturate(_Color.a * (0.18 + emission));
                float3 color = _Color.rgb * _EmissionIntensity * (0.25 + emission * 1.8);
                return fixed4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Unlit/Transparent"
}
