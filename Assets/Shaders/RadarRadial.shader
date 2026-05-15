Shader "Unlit/RadarRadial"
{
    Properties{
        _Color("Color", Color) = (0.95,0.22,0.06,0.35)
        _Length("Length", Float) = 300
        _Repeat("Repeat", Float) = 30
        _Offset("Offset", Float) = 0
        _Thickness("Thickness", Range(0,1)) = 0.3
        _ScanTime("Scan Time", Float) = 0
    }
    SubShader{
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Pass{
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 localPos : TEXCOORD0; };

            float4 _Color;
            float _Length;
            float _Repeat;
            float _Offset;
            float _Thickness;
            float _ScanTime;

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                float normalizedHeight = saturate(i.localPos.z / max(0.0001, _Length));
                float phase = normalizedHeight * max(0.0001, _Repeat) + _Offset - _ScanTime;
                float wave = frac(phase);
                float band = smoothstep(1.0 - _Thickness, 1.0, wave);
                fixed3 col = _Color.rgb;
                fixed alpha = band * _Color.a;
                return fixed4(col, alpha);
            }

            ENDHLSL
        }
    }
    FallBack "Unlit/Transparent"
}
