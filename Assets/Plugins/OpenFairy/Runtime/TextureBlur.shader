Shader "OpenFairy/Texture Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float2 _Direction;
        float _Unpremultiply;

        float4 Premultiply(v2f_img input) : SV_Target
        {
            float4 color = tex2D(_MainTex, input.uv);
            return float4(color.rgb * color.a, color.a);
        }

        float4 Blur(v2f_img input) : SV_Target
        {
            float4 color = tex2D(_MainTex, input.uv) * 0.227027027;
            color += (tex2D(_MainTex, input.uv + _Direction) + tex2D(_MainTex, input.uv - _Direction)) * 0.194594595;
            color += (tex2D(_MainTex, input.uv + _Direction * 2) + tex2D(_MainTex, input.uv - _Direction * 2)) * 0.121621622;
            color += (tex2D(_MainTex, input.uv + _Direction * 3) + tex2D(_MainTex, input.uv - _Direction * 3)) * 0.054054054;
            color += (tex2D(_MainTex, input.uv + _Direction * 4) + tex2D(_MainTex, input.uv - _Direction * 4)) * 0.016216216;
            if (_Unpremultiply > 0)
                color.rgb = color.a > 0 ? color.rgb / color.a : 0;
            return color;
        }
        ENDCG
        Pass
        {
            Name "Premultiply"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Premultiply
            ENDCG
        }
        Pass
        {
            Name "Blur"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Blur
            ENDCG
        }
    }
    Fallback Off
}
