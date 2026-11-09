Shader "BlockBlast/Bullet Trail"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _BoardClipRect ("Board Bounds", Vector) = (-32767,-32767,32767,32767)
        [HideInInspector] _BoardClipEnabled ("Clip To Board", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
            };
            fixed4 _Color;
            float4 _BoardClipRect;
            float _BoardClipEnabled;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                if (_BoardClipEnabled > 0.5)
                {
                    float2 insideMin = i.worldPosition - _BoardClipRect.xy;
                    float2 insideMax = _BoardClipRect.zw - i.worldPosition;
                    clip(min(min(insideMin.x, insideMin.y), min(insideMax.x, insideMax.y)));
                }
                fixed4 color = i.color;
                // Feather both edges of the ribbon without needing a texture.
                color.a *= pow(saturate(1.0 - abs(i.uv.y * 2.0 - 1.0)), 1.5);
                return color;
            }
            ENDCG
        }
    }
}
