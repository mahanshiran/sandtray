// Blends seven surface materials using a primary RGBA map and an extra RG map.
Shader "Sandplay/SandSplat"
{
    Properties
    {
        _MainTex      ("Base Texture (Sand)", 2D) = "white" {}
        _BumpMap      ("Normal Map", 2D) = "bump" {}
        _BumpScale    ("Normal Scale", Float) = 0.8
        _DetailTex    ("Detail Texture", 2D) = "grey" {}

        _SplatMap     ("Splat Map", 2D) = "black" {}
        _ExtraSplatMap ("Extra Splat Map", 2D) = "black" {}

        // Per-material albedo colours
        _Color0 ("Sand Color",  Color) = (0.70, 0.60, 0.45, 1)
        _Color1 ("Rock Color",  Color) = (0.40, 0.38, 0.36, 1)
        _Color2 ("Grass Color", Color) = (0.24, 0.46, 0.22, 1)
        _Color3 ("Snow Color",  Color) = (0.88, 0.92, 0.96, 1)
        _Color4 ("Mud Color",   Color) = (0.32, 0.24, 0.16, 1)
        _Color5 ("Water Color", Color) = (0.16, 0.52, 0.76, 1)
        _Color6 ("Clay Color",  Color) = (0.62, 0.34, 0.22, 1)

        // Per-material smoothness
        _Smooth0 ("Sand Smoothness",  Range(0,1)) = 0.08
        _Smooth1 ("Rock Smoothness",  Range(0,1)) = 0.20
        _Smooth2 ("Grass Smoothness", Range(0,1)) = 0.12
        _Smooth3 ("Snow Smoothness",  Range(0,1)) = 0.55
        _Smooth4 ("Mud Smoothness",   Range(0,1)) = 0.35
        _Smooth5 ("Water Smoothness", Range(0,1)) = 0.82
        _Smooth6 ("Clay Smoothness",  Range(0,1)) = 0.18
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _DetailTex;
        sampler2D _SplatMap;
        sampler2D _ExtraSplatMap;
        float     _BumpScale;

        fixed4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5, _Color6;
        float  _Smooth0, _Smooth1, _Smooth2, _Smooth3, _Smooth4, _Smooth5, _Smooth6;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_SplatMap; // splat uses its own (untiled) UV scaling
            float2 uv_ExtraSplatMap;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Sample splat map with its OWN UV (kept at 0-1 across the mesh).
            // Using uv_MainTex would inherit the base sand tiling and cause the
            // splatmap to repeat — making paint strokes appear in many places.
            fixed4 splat = tex2D(_SplatMap, IN.uv_SplatMap);
            fixed4 extra = tex2D(_ExtraSplatMap, IN.uv_ExtraSplatMap);
            float w1 = splat.r; // rock
            float w2 = splat.g; // grass
            float w3 = splat.b; // snow
            float w4 = splat.a; // mud
            float w5 = extra.r; // water
            float w6 = extra.g; // clay
            float w0 = saturate(1.0 - w1 - w2 - w3 - w4 - w5 - w6); // sand

            // Blend albedo
            fixed3 albedo = w0 * _Color0.rgb
                          + w1 * _Color1.rgb
                          + w2 * _Color2.rgb
                          + w3 * _Color3.rgb
                          + w4 * _Color4.rgb
                          + w5 * _Color5.rgb
                          + w6 * _Color6.rgb;

            // Blend smoothness
            float smoothness = w0 * _Smooth0
                             + w1 * _Smooth1
                             + w2 * _Smooth2
                             + w3 * _Smooth3
                             + w4 * _Smooth4
                             + w5 * _Smooth5
                             + w6 * _Smooth6;

            // Tint by base texture for extra grain
            fixed4 baseTex = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 detail  = tex2D(_DetailTex, IN.uv_MainTex * 3.0);

            o.Albedo     = albedo * baseTex.rgb * (detail.rgb * 2.0);
            o.Smoothness = smoothness;
            o.Metallic   = 0;
            o.Normal     = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
        }
        ENDCG
    }

    FallBack "Diffuse"
}
