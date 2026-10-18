Shader "OpenFairy/UI ColorMatrix"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _BlendSrcFactor ("Blend SrcFactor", Float) = 5
        _BlendDstFactor ("Blend DstFactor", Float) = 10
        _BlendSrcFactorAlpha ("Blend SrcFactor Alpha", Float) = 1
        _BlendDstFactorAlpha ("Blend DstFactor Alpha", Float) = 10
        _ColorOption ("Premultiply Alpha", Float) = 0
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _UIMaskSoftnessX ("Mask Softness X", Float) = 0
        _UIMaskSoftnessY ("Mask Softness Y", Float) = 0
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_BlendSrcFactor] [_BlendDstFactor], [_BlendSrcFactorAlpha] [_BlendDstFactorAlpha]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            float4 _ClipRect;
            float _ColorOption;
            float4x4 _ColorMatrix;
            float4 _ColorOffset;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                half4 mask : TEXCOORD1;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 pixelSize = o.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = half4(v.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                o.texcoord = v.texcoord;
#if !defined(UNITY_COLORSPACE_GAMMA) && (UNITY_VERSION >= 550)
                o.color.rgb = GammaToLinearSpace(v.color.rgb);
                o.color.a = v.color.a;
#else
                o.color = v.color;
#endif
                o.color *= _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                fixed4 result;
                result.r = dot(col, _ColorMatrix[0]) + _ColorOffset.x;
                result.g = dot(col, _ColorMatrix[1]) + _ColorOffset.y;
                result.b = dot(col, _ColorMatrix[2]) + _ColorOffset.z;
                result.a = dot(col, _ColorMatrix[3]) + _ColorOffset.w;
#ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                result.a *= mask.x * mask.y;
#endif
#ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
#endif
                if (_ColorOption != 0)
                    result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
        // RGB transforms linearly in PMA space; coverage weights the color offset.
        Pass
        {
            ZTest Always
            Blend Off
            Stencil { Comp Always }
            ColorMask RGBA
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_pma
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4x4 _ColorMatrix;
            float4 _ColorOffset;
            float4 frag_pma(v2f_img input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                return mul(_ColorMatrix, color) + _ColorOffset * color.a;
            }
            ENDCG
        }
    }
}
