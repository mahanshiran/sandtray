Shader "Sandplay/WindowSky"
{
    Properties
    {
        _MainTex ("Sunny Landscape", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Map distant scenery by viewing direction, like a skybox.
                // The horizon stays at eye level and the frame reveals different
                // scenery as the viewer moves, instead of stretching a poster.
                float3 direction = normalize(i.worldPosition - _WorldSpaceCameraPos);
                float2 uv = float2(.5 + atan2(direction.z, direction.x) / 2.4,
                                   .32 + asin(clamp(direction.y, -.999, .999)) / 1.2);

                float3 scenery = tex2D(_MainTex, saturate(uv)).rgb;
                // Extend below the photograph with softly shaded meadow,
                // rather than stretching its bottom row down steep views.
                float3 meadow = float3(.48, .53, .35);
                meadow *= .94 + .06 * sin(direction.z * 27 + direction.y * 19);
                scenery = lerp(meadow, scenery, smoothstep(.04, .16, uv.y));
                scenery = lerp(scenery, float3(.68, .83, .94), smoothstep(.94, 1.04, uv.y));
                float3 outside = lerp(meadow, float3(.68, .83, .94), smoothstep(-.08, .1, direction.y));
                float edges = smoothstep(-.05, .05, uv.x) * (1 - smoothstep(.95, 1.05, uv.x));
                scenery = lerp(outside, scenery, edges);
                // Indoor exposure makes distant daylight bright and slightly
                // hazy, rather than a saturated photograph mounted on the wall.
                float luminance = dot(scenery, float3(.2126, .7152, .0722));
                scenery = lerp(luminance.xxx, scenery, .68);
                scenery = lerp(scenery * 1.12, float3(.86, .92, .97), .16);
                return fixed4(scenery, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
