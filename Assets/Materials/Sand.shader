Shader "Sandplay/Sand"
{
    Properties
    {
        _Color ("Sand Color", Color) = (0.96, 0.87, 0.70, 1)
        _DarkColor ("Shadow Color", Color) = (0.75, 0.63, 0.42, 1)
        _MainTex ("Grain Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.5
        _Glossiness ("Smoothness", Range(0, 1)) = 0.15
        _GrainScale ("Grain Scale", Range(1, 50)) = 15
        _HeightBlend ("Height Blend", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NormalMap;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        fixed4 _Color;
        fixed4 _DarkColor;
        half _Glossiness;
        half _NormalStrength;
        float _GrainScale;
        float _HeightBlend;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Triplanar-ish grain using world position for tiling
            float2 grainUV = IN.worldPos.xz * _GrainScale;
            fixed4 grain = tex2D(_MainTex, grainUV);

            // Height-based color blend (valleys darker, peaks lighter)
            float heightFactor = saturate(IN.worldPos.y * _HeightBlend);
            fixed4 sandColor = lerp(_DarkColor, _Color, heightFactor);

            o.Albedo = sandColor.rgb * lerp(0.85, 1.15, grain.r);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;

            // Normal map for grain detail
            float3 normal = UnpackNormal(tex2D(_NormalMap, grainUV));
            normal.xy *= _NormalStrength;
            o.Normal = normalize(normal);

            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
