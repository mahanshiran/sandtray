Shader "Sandplay/SandBrushPreview"
{
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct Output { float4 position:SV_POSITION; fixed4 color:COLOR; };
            Output vert(Input v) { Output o; o.position=UnityObjectToClipPos(v.vertex); o.color=v.color; return o; }
            fixed4 frag(Output i):SV_Target { return i.color; }
            ENDCG
        }
    }
}
