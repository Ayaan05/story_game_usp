Shader "Unlit/BlackOnlyTransparent"
{
    Properties
    {
        _MainTex ("Video (RenderTexture)", 2D) = "white" {}
        _Epsilon ("Almost-Black Tolerance", Range(0,0.02)) = 0.0 // keep 0.0 for strict #000000 only
        _Smooth  ("Edge Smooth (when epsilon>0)", Range(0,0.2)) = 0.02
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Epsilon;
            float _Smooth;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);

                // max channel; 0 only when exactly #000000
                float maxc = max(max(c.r, c.g), c.b);
                float alpha;

                if (_Epsilon <= 0.0)
                {
                    // STRICT: remove only exact #000000
                    // (maxc == 0) -> alpha = 0, else 1
                    alpha = (maxc == 0.0) ? 0.0 : 1.0;
                }
                else
                {
                    // NEARLY-EXACT: treat very-near-black as transparent
                    // Smooth edge for nicer halos when epsilon>0
                    alpha = smoothstep(_Epsilon, _Epsilon + _Smooth, maxc);
                }

                return float4(c.rgb, alpha);
            }
            ENDCG
        }
    }
}
