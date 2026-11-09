Shader "Game/Dual Block Masked Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _MaskTex ("Authored Half Mask", 2D) = "white" {}
        [PerRendererData] _MaskUvX ("Mask U Mapping", Vector) = (1,0,0,0)
        [PerRendererData] _MaskUvY ("Mask V Mapping", Vector) = (0,1,0,0)
        [PerRendererData] _MaskLocalRect ("Mask Local Bounds", Vector) = (-1,-1,1,1)
        [PerRendererData] _MaskCutoff ("Mask Alpha Cutoff", Float) = 0.5
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
            "DisableBatching"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex DualSpriteVert
            #pragma fragment DualSpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            sampler2D _MaskTex;
            float4 _MaskUvX;
            float4 _MaskUvY;
            float4 _MaskLocalRect;
            float _MaskCutoff;

            struct dual_v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                float2 localPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            dual_v2f DualSpriteVert(appdata_t input)
            {
                dual_v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 localVertex = UnityFlipSprite(input.vertex.xyz, _Flip);
                output.vertex = UnityObjectToClipPos(localVertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color * _RendererColor;
                float3 maskPosition = float3(localVertex.xy, 1);
                output.maskUV = float2(dot(maskPosition, _MaskUvX.xyz), dot(maskPosition, _MaskUvY.xyz));
                output.localPosition = localVertex.xy;
                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif
                return output;
            }

            fixed4 DualSpriteFrag(dual_v2f input) : SV_Target
            {
                float2 insideMin = input.localPosition - _MaskLocalRect.xy;
                float2 insideMax = _MaskLocalRect.zw - input.localPosition;
                clip(min(min(insideMin.x, insideMin.y), min(insideMax.x, insideMax.y)));
                clip(tex2D(_MaskTex, input.maskUV).a - _MaskCutoff);
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
