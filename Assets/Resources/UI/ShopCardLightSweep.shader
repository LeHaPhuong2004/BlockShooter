Shader "UI/Shop Card Light Sweep"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.98, 0.91, 1)
        [HideInInspector] _TextureSampleAdd ("Texture Sample Add", Color) = (0, 0, 0, 0)
        _SweepTime ("Unscaled Animation Time", Float) = 0
        _SweepDuration ("Sweep Duration", Float) = 1.1
        _SweepInterval ("Sweep Interval", Float) = 4.4
        _Intensity ("Light Opacity", Range(0, 1)) = 0.64
        _BandWidth ("Band Width", Range(0.01, 0.5)) = 0.2
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "ShopCardLightSweep"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                // xy are normalized card coordinates; z is the per-card cycle offset.
                float4 cardData : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                half4 mask : TEXCOORD1;
                half4 sweep : TEXCOORD2;
                half twinkle : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;
            fixed4 _Color;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            float _SweepTime;
            float _SweepDuration;
            float _SweepInterval;
            half _Intensity;
            half _BandWidth;

            half Pulse(float phase)
            {
                half value = saturate(1.0 - abs(frac(phase) * 2.0 - 1.0));
                return value * value;
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = float4(TRANSFORM_TEX(input.texcoord, _MainTex), input.cardData.xy);

                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clipRect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = half4(input.vertex.xy * 2.0 - clipRect.xy - clipRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));

                float phase = input.cardData.z;
                float interval = max(0.2, _SweepInterval);
                float duration = clamp(_SweepDuration, 0.1, interval);
                float elapsed = frac(_SweepTime / interval + phase) * interval;
                output.sweep.x = lerp(-0.3, 1.68, saturate(elapsed / duration));
                output.sweep.y = smoothstep(0.0, duration * 0.1, elapsed)
                    * (1.0 - smoothstep(duration * 0.85, duration, elapsed));
                // Pulse amplitudes are constant across each card and evaluated per vertex.
                output.sweep.z = Pulse(_SweepTime * 0.31 + phase + 0.12);
                output.sweep.w = Pulse(_SweepTime * 0.37 + phase + 0.63);
                output.twinkle = Pulse(_SweepTime * 0.29 + phase + 0.38);
                return output;
            }

            half2 Star(half2 position, half2 center)
            {
                half2 distance = abs((position - center) / half2(0.043, 0.062));
                // Concave sides taper into four long points, around a luminous center.
                half points = saturate(1.0 - distance.x - distance.y - distance.x * distance.y * 3.2);
                half squaredRadius = dot(distance, distance);
                half core = saturate(1.0 - squaredRadius * 18.0);
                half halo = saturate(1.0 - squaredRadius * 0.55);
                return half2(saturate(points * 0.9 + core * 0.8), halo * halo);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                half2 local = input.uv.zw;
                half distance = abs(local.x + local.y * 0.38 - input.sweep.x) / max(0.01, _BandWidth);
                half broad = saturate(1.0 - distance);
                broad = broad * broad * (3.0 - 2.0 * broad);
                half core = saturate(1.0 - distance * 4.0);
                half glow = saturate(1.0 - distance * 0.55);
                half sweep = (broad * 0.5 + core * core * 0.65 + glow * glow * 0.15) * input.sweep.y;
                half2 sparkle = Star(local, half2(0.14, 0.83)) * input.sweep.z
                    + Star(local, half2(0.86, 0.85)) * input.sweep.w
                    + Star(local, half2(0.79, 0.16)) * input.twinkle;

                half silhouette = saturate(tex2D(_MainTex, input.uv.xy).a + _TextureSampleAdd.a);
                half alpha = silhouette * input.color.a * _Intensity
                    * saturate(sweep + sparkle.x * 1.25 + sparkle.y * 0.28);
                half goldenHalo = saturate(sparkle.y - sparkle.x * 2.0 - sweep * 1.4);
                half3 lightColor = lerp(input.color.rgb, half3(1.0, 0.8, 0.3), goldenHalo * 0.65);
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                alpha *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(lightColor, alpha);
            }
            ENDCG
        }
    }
}
