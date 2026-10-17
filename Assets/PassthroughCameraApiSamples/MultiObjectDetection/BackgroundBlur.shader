Shader "Custom/BackgroundBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 2.0
        _PixelSize ("Pixel Size", Float) = 20.0
        _Alpha ("Alpha", Range(0,1)) = 0.4
        _ScreenRect ("TV Rect (x,y,width,height)", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 100

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _BlurSize;
            float _PixelSize;
            float _Alpha;
            float4 _ScreenRect;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // Only apply effect inside TV rect
                if (uv.x < _ScreenRect.x || uv.x > (_ScreenRect.x + _ScreenRect.z) ||
                    uv.y < _ScreenRect.y || uv.y > (_ScreenRect.y + _ScreenRect.w))
                {
                    return tex2D(_MainTex, uv); // outside TV, normal
                }

                // Map UV to TV rect
                float2 rectUV = (uv - _ScreenRect.xy) / _ScreenRect.zw;

                // Pixelation
                float2 pixelUV = floor(rectUV * _PixelSize) / _PixelSize;
                pixelUV = pixelUV * _ScreenRect.zw + _ScreenRect.xy;

                // 9-tap box blur
                fixed4 col = fixed4(0,0,0,0);
                float2 offset = _BlurSize / _ScreenParams.xy;

                col += tex2D(_MainTex, pixelUV + float2(-offset.x, -offset.y)) * 0.111;
                col += tex2D(_MainTex, pixelUV + float2(0, -offset.y)) * 0.111;
                col += tex2D(_MainTex, pixelUV + float2(offset.x, -offset.y)) * 0.111;
                col += tex2D(_MainTex, pixelUV + float2(-offset.x, 0)) * 0.111;
                col += tex2D(_MainTex, pixelUV) * 0.111;
                col += tex2D(_MainTex, pixelUV + float2(offset.x, 0)) * 0.111;
                col += tex2D(_MainTex, pixelUV + float2(-offset.x, offset.y)) * 0.111;
                col += tex2D(_MainTex, pixelUV + float2(0, offset.y)) * 0.111;
                col += tex2D(_MainTex, pixelUV + float2(offset.x, offset.y)) * 0.111;

                col.a = _Alpha; // semi-transparent ghost

                return col;
            }
            ENDCG
        }
    }
}