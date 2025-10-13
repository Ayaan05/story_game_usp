Shader "Unlit/DogGlobalBlendBlackKey"
{
    Properties
    {
        // Added so RawImage/UI can set it without errors (not used)
        _MainTex     ("UI/MainTex (unused)", 2D) = "white" {}

        _DirtyTex     ("Dirty RT", 2D) = "black" {}
        _CleanTex     ("Clean RT", 2D) = "black" {}
        _Cleanliness  ("Cleanliness (0..1)", Range(0,1)) = 0
        _Epsilon      ("Black Tolerance", Range(0,0.02)) = 0.002
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

            // UI will set this; we ignore it, but having it prevents the spam
            sampler2D _MainTex;

            sampler2D _DirtyTex, _CleanTex;
            float _Cleanliness;
            float _Epsilon;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(appdata v){
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
            }

            float isVisible(float3 c, float eps)
            {
                // 0 when near-black, 1 otherwise
                return step(eps, max(max(c.r, c.g), c.b));
            }

            fixed4 frag(v2f i):SV_Target
            {
                float3 d = tex2D(_DirtyTex, i.uv).rgb;
                float3 c = tex2D(_CleanTex, i.uv).rgb;

                float3 mixRGB = lerp(d, c, saturate(_Cleanliness));

                // Show pixel if either source is not near-black
                float alpha = saturate(max(isVisible(d, _Epsilon), isVisible(c, _Epsilon)));

                return float4(mixRGB, alpha);
            }
            ENDCG
        }
    }
}
