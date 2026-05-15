Shader "Unlit/GroundRadarDomeScan"
{
    Properties
    {
        _Color("Color", Color) = (0.22, 0.62, 1, 0.22)
        _Radius("Radius", Float) = 26000
        _ScanTime("Scan Time", Float) = 0
        _Repeat("Repeat", Float) = 30
        _Thickness("Thickness", Range(0.01, 0.5)) = 0.08
        _Offset("Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
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
            float _Radius;
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
                float radius = max(0.0001, _Radius);
                // 归一化高度 [0, 1]，1 为顶部，0 为底部
                float height01 = saturate(i.localPos.y / radius);

                // 环状扫描：扫描线从顶部向下推进
                // _ScanTime 从 0 到 1 循环，表示扫描进度
                float scanProgressFromTop = frac(_ScanTime);
                
                // 扫描线位置（从上向下）
                float scanLineY = 1.0 - scanProgressFromTop;

                // 计算距离扫描线的"环距离"
                // 用 _Repeat 个周期覆盖整个视锥
                float sp = 1.0 / max(0.0001, _Repeat);
                float phase = height01 - _Offset - _ScanTime;
                float wave = frac(phase / sp);
                
                // 扫描环的宽度和硬度
                float thicknessUV = max(_Thickness, 0.0001);
                float bandStrength = step(1.0 - thicknessUV, wave);
                float glow = 1.0 - smoothstep(0.0, thicknessUV, abs(wave - (1.0 - thicknessUV * 0.5)));

                // 主要环的亮度
                fixed3 ringCol = _Color.rgb * bandStrength * 1.5;
                // 发光环
                fixed3 glowCol = _Color.rgb * glow * 0.7;
                
                fixed3 finalCol = (_Color.rgb * 0.25) + ringCol + glowCol;
                
                // Alpha 合成
                fixed baseAlpha = _Color.a * 0.18;
                fixed ringAlpha = _Color.a * bandStrength * 0.85;
                fixed glowAlpha = glow * _Color.a * 0.35;
                fixed finalAlpha = saturate(baseAlpha + ringAlpha + glowAlpha);

                return fixed4(finalCol, finalAlpha);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}