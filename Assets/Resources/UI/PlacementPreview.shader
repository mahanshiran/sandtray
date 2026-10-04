Shader "Sandplay/PlacementPreview"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Color", Color) = (1,1,1,.65) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float light:TEXCOORD1; };
            v2f vert(appdata_base v) {
                v2f o; o.position=UnityObjectToClipPos(v.vertex);
                o.uv=TRANSFORM_TEX(v.texcoord,_MainTex);
                float3 n=normalize(UnityObjectToWorldNormal(v.normal)+float3(0,.00001,0));
                o.light=.65+.35*saturate(dot(n,normalize(float3(.4,1,-.3)))); return o;
            }
            fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv)*_Color; c.rgb*=i.light; return c; }
            ENDCG
        }
    }
}
