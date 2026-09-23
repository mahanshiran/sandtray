Shader "UI/DialogBackdrop"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        // Capture what has already been drawn, including UI behind this dialog.
        GrabPass { }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float4 grab : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeGrabScreenPos(o.vertex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.grab.xy / i.grab.w;
                float2 stepSize = abs(_GrabTexture_TexelSize.xy) * 8.0;
                fixed3 color = 0;
                [unroll] for (int x = -1; x <= 1; x++)
                    [unroll] for (int y = -1; y <= 1; y++)
                        color += tex2D(_GrabTexture, saturate(uv + float2(x,y) * stepSize)).rgb;
                return fixed4(color * (0.28 / 9.0), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
