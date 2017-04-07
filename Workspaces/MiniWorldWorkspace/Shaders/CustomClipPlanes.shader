﻿// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "EditorVR/Custom/Custom Clip Planes"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf NoLighting nolightmap noforwardadd noshadow nometa vertex:vert

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input
		{
			float2 uv_MainTex;
			float3 clipPos;
		};
		static const fixed4 white = fixed4(1, 1, 1, 1);

		float4 _GlobalClipCenter;
		float4 _GlobalClipExtents;
		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
		float4x4 _InverseRotation;

		fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten) { return fixed4(0, 0, 0, 0); }

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.clipPos = mul(_InverseRotation, mul(unity_ObjectToWorld, v.vertex));
		}

		void surf(Input IN, inout SurfaceOutput o)
		{
			// Clip if position is outside of clip bounds
			float3 diff = abs(IN.clipPos - _GlobalClipCenter);
			if (diff.x > _GlobalClipExtents.x || diff.y > _GlobalClipExtents.y || diff.z > _GlobalClipExtents.z)
				discard;

			// Some materials don't have colors set, so default them to white
			if (dot(_Color, white) <= 0)
				_Color = white;

			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) *_Color;
			o.Emission = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}
}