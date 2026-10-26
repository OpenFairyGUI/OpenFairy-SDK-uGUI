Shader "OpenFairy/UI Premultiplied"
{
    Properties
    {
        _OpenFairyGamma ("Gamma UI", Float) = 0
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _BlendSrcFactorAlpha ("Alpha Source", Float) = 1
        _BlendDstFactorAlpha ("Alpha Destination", Float) = 10
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _UIMaskSoftnessX ("Mask Softness X", Float) = 0
        _UIMaskSoftnessY ("Mask Softness Y", Float) = 0
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha, [_BlendSrcFactorAlpha] [_BlendDstFactorAlpha]
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            struct vertex { float4 position:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct pixel { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; half4 mask:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _Color, _TextureSampleAdd, _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            pixel vert(vertex input)
            {
                pixel output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color * _Color;
                output.uv = input.uv;
                float2 pixelSize = output.position.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = half4(input.position.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }
            fixed4 frag(pixel input):SV_Target
            {
                fixed4 texel = tex2D(_MainTex, input.uv);
                half opacity = input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                opacity *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(texel.a * opacity - 0.001);
                #endif
                return fixed4(texel.rgb * input.color.rgb * opacity, texel.a * opacity);
            }
            ENDCG
        }
    }
}
