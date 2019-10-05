Shader "Hidden/MixtureTextureCubePreview"
{
    Properties
    {
        _Cubemap ("Texture", Cube) = "" {}
        _Slice ("Slice", Float) = 0
		_Channels("_Channels", Vector) = (1.0,1.0,1.0,1.0)
		_PreviewMip("_PreviewMip", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Overlay" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off
			ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "MixtureUtils.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
				float2 clipUV : TEXCOORD1;
            };

            UNITY_DECLARE_TEXCUBE(_Cubemap);
            float4 _Cubemap_ST;
            float _Slice;
			float4 _Channels;
			float _PreviewMip;

			uniform float4x4 unity_GUIClipTextureMatrix;
			sampler2D _GUIClipTexture;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _Cubemap);

				float3 screenUV = UnityObjectToViewPos(v.vertex);
				o.clipUV = mul(unity_GUIClipTextureMatrix, float4(screenUV.xy, 0, 1.0));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				float2 checkerboardUVs = ceil(fmod(i.uv * 16.0, 1.0) - 0.5);
				float3 checkerboard = lerp(0.3,0.4, checkerboardUVs.x != checkerboardUVs.y ? 1 : 0);

				float4 color = UNITY_SAMPLE_TEXCUBE_LOD(_Cubemap, LatlongToDirectionCoordinate(i.uv), _PreviewMip) * _Channels;

				if (_Channels.a == 0.0)
					color.a = 1.0;

				else if (_Channels.r == 0.0 && _Channels.g == 0.0 && _Channels.b == 0.0 && _Channels.a == 1.0)
				{
					color.rgb = color.a;
					color.a = 1.0;
				}
				color.xyz = pow(color.xyz, 1.0 / 2.2);

				float clip = tex2D(_GUIClipTexture, i.clipUV).a;
				return saturate(float4(lerp(checkerboard, color.xyz, color.a), clip));

            }
            ENDCG
        }
    }
}
