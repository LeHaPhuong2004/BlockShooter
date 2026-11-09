Shader "Game/Board Projectile Sprite"
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
        [PerRendererData] _BoardClipRect ("Flight Bounds", Vector) = (-32767,-32767,32767,32767)
        [PerRendererData] _BoardClipEnabled ("Clip To Flight Bounds", Float) = 0
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
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex ProjectileVert
            #pragma fragment ProjectileFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float4 _BoardClipRect;
            float _BoardClipEnabled;

            struct projectile_v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            projectile_v2f ProjectileVert(appdata_t input)
            {
                projectile_v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 localVertex = UnityFlipSprite(input.vertex.xyz, _Flip);
                output.vertex = UnityObjectToClipPos(localVertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color * _RendererColor;
                output.worldPosition = mul(unity_ObjectToWorld, localVertex).xy;
                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif
                return output;
            }

            fixed4 ProjectileFrag(projectile_v2f input) : SV_Target
            {
                if (_BoardClipEnabled > 0.5)
                {
                    float2 insideMin = input.worldPosition - _BoardClipRect.xy;
                    float2 insideMax = _BoardClipRect.zw - input.worldPosition;
                    clip(min(min(insideMin.x, insideMin.y), min(insideMax.x, insideMax.y)));
                }
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
