Shader "OpenFairy/UI Blend"
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
        _BlendSrcFactorAlpha ("Blend SrcFactor Alpha", Float) = 5
        _BlendDstFactorAlpha ("Blend DstFactor Alpha", Float) = 10
        _ColorOption ("Premultiply Alpha", Float) = 0
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _ColorOption;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;

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
                float4 worldPosition : TEXCOORD1;
            };

            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.worldPosition.zw = 2 * abs(o.vertex.w / mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                o.texcoord = v.texcoord;
                // 同 uGUI UI-Default：线性空间下 Canvas 已在 CPU 把顶点色转为线性，仅 vertexColorAlwaysGammaSpace 时由着色器转换。
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                float2 distance = min(i.worldPosition.xy - _ClipRect.xy, _ClipRect.zw - i.worldPosition.xy);
                float2 softness = float2(_UIMaskSoftnessX, _UIMaskSoftnessY);
                // FairyGUI's default stage camera has an orthographic half-height of five.
                // Its soft-clip ramp spans softness / 5 screen pixels and uses the minimum axis.
                float2 factor = float2(
                    softness.x > 0 ? distance.x * 5 / (softness.x * i.worldPosition.z) : step(0, distance.x),
                    softness.y > 0 ? distance.y * 5 / (softness.y * i.worldPosition.w) : step(0, distance.y));
                color.a *= saturate(min(factor.x, factor.y));
                if (_ColorOption != 0)
                    color.rgb *= color.a;
#ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
#endif
                return color;
            }
            ENDCG
        }
    }
}
