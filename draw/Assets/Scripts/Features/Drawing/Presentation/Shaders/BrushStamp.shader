Shader "Drawing/BrushStamp"
{
    Properties
    {
        _MainTex ("Brush Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Int) = 1 // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Int) = 10 // OneMinusSrcAlpha
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Op", Int) = 0 // Add
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        // Configurable Blend Mode & Operation
        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend]
        
        // No depth write/test for 2D drawing
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // Vertex color (from C# SetColors)
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _UseProcedural;
            float _EdgeSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Combine material color and vertex color (alpha/pressure)
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texCol = tex2D(_MainTex, i.uv);
                
                // Final alpha = TextureAlpha * VertexAlpha * TintAlpha
                float alpha = texCol.a * i.color.a;
                
                // Final RGB = TextureRGB * VertexRGB * TintRGB
                float3 rgb = texCol.rgb * i.color.rgb;

                // PREMULTIPLY ALPHA: Multiply RGB by Alpha before output
                // This matches "Blend One OneMinusSrcAlpha"
                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
