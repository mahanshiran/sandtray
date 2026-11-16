Shader "Sandplay/WindowGlass"
{
    Properties
    {
        _Color ("Glass Tint", Color) = (.96, .98, 1, .035)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - i.worldPosition);
                float3 normal = normalize(i.worldNormal);
                float grazing = pow(1 - saturate(dot(normal, view)), 5);
                half4 probe = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflect(-view, normal));
                half3 reflection = DecodeHDR(probe, unity_SpecCube0_HDR);
                // Subtle room reflection, with stronger glass at grazing angles.
                // No runtime keywords or stripped Standard shader variants.
                return fixed4(lerp(_Color.rgb, reflection, .15),
                    lerp(_Color.a, .18, grazing));
            }
            ENDCG
        }
    }
}
