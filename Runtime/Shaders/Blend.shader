Shader "Hidden/Mixture/Blend"
{	
	Properties
	{
		// Parameters for 2D
		[InlineTexture]_Source_2D("Source", 2D) = "white" {}
		[InlineTexture]_Target_2D("Target", 2D) = "white" {}
		[InlineTexture]_Mask_2D("Mask", 2D) = "white" {}

		// Parameters for 3D
		[InlineTexture]_Source_3D("Source", 3D) = "white" {}
		[InlineTexture]_Target_3D("Target", 3D) = "white" {}
		[InlineTexture]_Mask_3D("Mask", 3D) = "white" {}

		// Parameters for Cubemaps
		[InlineTexture]_Source_Cube("Source", Cube) = "white" {}
		[InlineTexture]_Target_Cube("Target", Cube) = "white" {}
		[InlineTexture]_Mask_Cube("Mask", Cube) = "white" {}

		// Common parameters
		[Enum(Blend,0,Additive,1,Multiplicative,2)]_BlendMode("Blend Mode", Float) = 0
		[Enum(Alpha,0,PerChannel,1)]_MaskMode("Mask Mode", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#include "MixtureFixed.cginc"
            #pragma vertex CustomRenderTextureVertexShader
			#pragma fragment mixture
			#pragma target 3.0

            #pragma multi_compile CRT_2D CRT_3D CRT_CUBE

			// This macro will declare a version for each dimention (2D, 3D and Cube)
			TEXTURE_SAMPLER_X(_Source);
			TEXTURE_SAMPLER_X(_Target);
			TEXTURE_SAMPLER_X(_Mask);

			float _BlendMode;
			float _MaskMode;

			float4 mixture (v2f_customrendertexture i) : SV_Target
			{
				float4	source = SAMPLE_X(_Source, i.localTexcoord.xyz, i.direction);
				float4	target = SAMPLE_X(_Target, i.localTexcoord.xyz, i.direction);
				float4	mask = 0;
				
				switch((uint)_MaskMode)
				{
					case 0 : mask = SAMPLE_X(_Mask, i.localTexcoord.xyz, i.direction).aaaa; break;
					case 1 : mask = SAMPLE_X(_Mask, i.localTexcoord.xyz, i.direction).rgba; break;
				}

				switch ((uint)_BlendMode)
				{
					default:
					case 0: return lerp(source, target, mask);
					case 1: return lerp(source, source + target, mask);
					case 2: return lerp(source, source * target, mask);
				}

				// Should not happen but hey...
				return float4(1.0, 0.0, 1.0, 1.0);
			}
			ENDCG
		}
	}
}
