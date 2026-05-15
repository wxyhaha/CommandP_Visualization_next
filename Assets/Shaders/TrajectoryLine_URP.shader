Shader "CommandP/TrajectoryLine_URP"
{
    Properties
    {
        [Header(Base Color)]
        _BaseColor ("Base Color", Color) = (0.1, 0.9, 1, 1)
        [HDR] _TipColor ("Tip / Head Glow Color", Color) = (1, 1, 0.6, 1)
        _MainTex ("Flow Texture", 2D) = "white" {}

        [Header(Flow)]
        _FlowSpeed ("Flow Speed", Float) = 0.8
        _FlowTexTiling ("Flow Texture Tiling", Float) = 4.0

        [Header(Alpha Gradient)]
        _AlphaHead ("Alpha Head", Range(0, 1)) = 0.95
        _AlphaTail ("Alpha Tail", Range(0, 1)) = 0.06
        _AlphaPower ("Alpha Power Curve", Range(0.3, 4)) = 1.3
        _WidthEdgeFade ("Width Edge Fade", Range(0, 1)) = 0.65

        [Header(Scan Highlight)]
        [Toggle] _ScanEnabled ("Scan Enabled", Float) = 0
        _ScanSpeed ("Scan Speed", Float) = 1.0
        _ScanWidth ("Scan Width", Range(0.005, 0.4)) = 0.07
        [HDR] _ScanColor ("Scan Color", Color) = (1, 1, 0.55, 1)
        _ScanGlowFalloff ("Scan Glow Falloff", Float) = 5.0

        [Header(Breath)]
        [Toggle] _BreathEnabled ("Breath Enabled", Float) = 0
        _BreathSpeed ("Breath Speed", Float) = 0.6
        _BreathStrength ("Breath Strength", Range(0, 0.5)) = 0.22

        [Header(Rendering)]
        _WidthMultiplier ("Width Multiplier", Float) = 1.0
        _EmissionIntensity ("Emission Intensity", Float) = 2.5
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
            Name "TrajectoryLineForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            // 使用 float toggle 而非 shader_feature：所有轨迹共享同一 Shader 变体，
            // 通过 MaterialPropertyBlock 逐实例开关扫描/呼吸效果。SRP Batcher 只需 1 个 Slot。
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #ifndef PI
            #define PI 3.14159265359
            #endif

            // ============================================================
            // Vertex / Fragment Structures
            // ============================================================
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float4 vertexColor : TEXCOORD2;
            };

            // ============================================================
            // Per-Material CBUFFER (SRP Batcher compatible)
            // ============================================================
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _MainTex_ST;
                float  _FlowSpeed;
                float  _FlowTexTiling;
                float  _AlphaHead;
                float  _AlphaTail;
                float  _AlphaPower;
                float  _WidthEdgeFade;
                float  _ScanEnabled;
                float  _ScanSpeed;
                float  _ScanWidth;
                float4 _ScanColor;
                float  _ScanGlowFalloff;
                float  _BreathEnabled;
                float  _BreathSpeed;
                float  _BreathStrength;
                float  _WidthMultiplier;
                float  _EmissionIntensity;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ============================================================
            // Vertex Shader
            // ============================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.vertexColor = IN.color;
                return OUT;
            }

            // ============================================================
            // Fragment Shader
            // ============================================================

            // 采样流动纹理，tiling 控制条纹密度
            float SampleFlow(float u, float v)
            {
                float2 flowUV = float2(u * _FlowTexTiling + _Time.y * _FlowSpeed, v * 0.5 + 0.5);
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUV);
                // 取亮度作为 flow mask
                return tex.a;   // 用 RGBA32 纹理的 alpha 通道存储亮度
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // 调试：直接输出顶点颜色，看是否能显示
                return IN.vertexColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialInspector"
}
