// Made with Amplify Shader Editor v1.9.9.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Nicrom/LPW/ASE/Low Poly Vegetation Transparent"
{
	Properties
	{
		_Color( "Color", Color ) = ( 1, 1, 1, 1 )
		[NoScaleOffset] _MainTex( "Main Tex", 2D ) = "white" {}
		[Space] _Metallic( "Metallic", Range( 0, 1 ) ) = 0
		_Smoothness( "Smoothness", Range( 0, 1 ) ) = 0
		_AlphaCutoff( "Alpha Cutoff", Range( 0, 1 ) ) = 0.5
		[Header(Main Bending)][Space] _MBDefaultBending( "MB Default Bending", Float ) = 0
		[Space] _MBAmplitude( "MB Amplitude", Float ) = 1.5
		_MBAmplitudeOffset( "MB Amplitude Offset", Float ) = 2
		[Space] _MBFrequency( "MB Frequency", Float ) = 1.11
		_MBFrequencyOffset( "MB Frequency Offset", Float ) = 0
		[Space] _MBPhase( "MB Phase", Float ) = 1
		[Space] _MBWindDir( "MB Wind Dir", Range( 0, 360 ) ) = 0
		_MBWindDirOffset( "MB Wind Dir Offset", Range( 0, 180 ) ) = 20
		[Space] _MBMaxHeight( "MB Max Height", Float ) = 10
		[NoScaleOffset][Header(World Space Noise)][Space] _NoiseTexture( "Noise Texture", 2D ) = "bump" {}
		_NoiseTextureTilling( "Noise Tilling - Static (XY), Animated (ZW)", Vector ) = ( 1, 1, 1, 1 )
		_NoisePannerSpeed( "Noise Panner Speed", Vector ) = ( 0.05, 0.03, 0, 0 )
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "AlphaTest+0" }
		Cull Off
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19908
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows vertex:vertexDataFunc 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform float _MBWindDir;
		uniform float _MBWindDirOffset;
		uniform sampler2D _NoiseTexture;
		uniform float4 _NoiseTextureTilling;
		uniform float2 _NoisePannerSpeed;
		uniform float _MBAmplitude;
		uniform float _MBAmplitudeOffset;
		uniform float _MBFrequency;
		uniform float _MBFrequencyOffset;
		uniform float _MBPhase;
		uniform float _MBDefaultBending;
		uniform float _MBMaxHeight;
		uniform sampler2D _MainTex;
		uniform float4 _Color;
		uniform float _Metallic;
		uniform float _Smoothness;
		uniform float _AlphaCutoff;


		float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
		{
			original -= center;
			float C = cos( angle );
			float S = sin( angle );
			float t = 1 - C;
			float m00 = t * u.x * u.x + C;
			float m01 = t * u.x * u.y - S * u.z;
			float m02 = t * u.x * u.z + S * u.y;
			float m10 = t * u.x * u.y + S * u.z;
			float m11 = t * u.y * u.y + C;
			float m12 = t * u.y * u.z - S * u.x;
			float m20 = t * u.x * u.z - S * u.y;
			float m21 = t * u.y * u.z + S * u.x;
			float m22 = t * u.z * u.z + C;
			float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
			return mul( finalMatrix, original ) + center;
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float MB_WindDirection40_g2 = _MBWindDir;
			float MB_WindDirectionOffset35_g2 = _MBWindDirOffset;
			float3 objToWorld2_g2 = mul( unity_ObjectToWorld, float4( float3( 0,0,0 ), 1 ) ).xyz;
			float2 appendResult4_g2 = (float2(objToWorld2_g2.x , objToWorld2_g2.z));
			float2 WorldSpaceUVs6_g2 = appendResult4_g2;
			float2 AnimatedNoiseTilling7_g2 = (_NoiseTextureTilling).zw;
			float2 panner11_g2 = ( 0.1 * _Time.y * _NoisePannerSpeed + float2( 0,0 ));
			float4 AnimatedWorldNoise18_g2 = tex2Dlod( _NoiseTexture, float4( ( ( WorldSpaceUVs6_g2 * AnimatedNoiseTilling7_g2 ) + panner11_g2 ), 0, 0.0) );
			float temp_output_65_0_g2 = radians( ( ( MB_WindDirection40_g2 + ( MB_WindDirectionOffset35_g2 *  (-1.0 + ( (AnimatedWorldNoise18_g2).r - 0.0 ) * ( 1.0 - -1.0 ) / ( 1.0 - 0.0 ) ) ) ) * -1.0 ) );
			float3 appendResult80_g2 = (float3(cos( temp_output_65_0_g2 ) , 0.0 , sin( temp_output_65_0_g2 )));
			float3 worldToObj84_g2 = mul( unity_WorldToObject, float4( appendResult80_g2, 1 ) ).xyz;
			float3 worldToObj81_g2 = mul( unity_WorldToObject, float4( float3( 0,0,0 ), 1 ) ).xyz;
			float3 normalizeResult88_g2 = normalize( ( worldToObj84_g2 - worldToObj81_g2 ) );
			float3 MB_RotationAxis91_g2 = normalizeResult88_g2;
			float MB_Amplitude59_g2 = _MBAmplitude;
			float MB_AmplitudeOffset51_g2 = _MBAmplitudeOffset;
			float2 StaticNoileTilling17_g2 = (_NoiseTextureTilling).xy;
			float4 StaticWorldNoise42_g2 = tex2Dlod( _NoiseTexture, float4( ( WorldSpaceUVs6_g2 * StaticNoileTilling17_g2 ), 0, 0.0) );
			float3 objToWorld46_g2 = mul( unity_ObjectToWorld, float4( float3( 0,0,0 ), 1 ) ).xyz;
			float MB_Frequency33_g2 = _MBFrequency;
			float MB_FrequencyOffset27_g2 = _MBFrequencyOffset;
			float MB_Phase50_g2 = _MBPhase;
			float MB_DefaultBending75_g2 = _MBDefaultBending;
			float3 ase_positionOS = v.vertex.xyz;
			float MB_MaxHeight73_g2 = _MBMaxHeight;
			float MB_RotationAngle92_g2 = radians( ( ( ( ( MB_Amplitude59_g2 + ( MB_AmplitudeOffset51_g2 * (StaticWorldNoise42_g2).r ) ) * sin( ( ( ( objToWorld46_g2.x + objToWorld46_g2.z ) + ( _Time.y * ( MB_Frequency33_g2 + ( MB_FrequencyOffset27_g2 * (StaticWorldNoise42_g2).r ) ) ) ) * MB_Phase50_g2 ) ) ) + MB_DefaultBending75_g2 ) * ( ase_positionOS.y / MB_MaxHeight73_g2 ) ) );
			float3 appendResult95_g2 = (float3(0.0 , ase_positionOS.y , 0.0));
			float3 rotatedValue98_g2 = RotateAroundAxis( appendResult95_g2, ase_positionOS, MB_RotationAxis91_g2, MB_RotationAngle92_g2 );
			float3 rotatedValue102_g2 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue98_g2, MB_RotationAxis91_g2, MB_RotationAngle92_g2 );
			v.vertex.xyz += ( ( rotatedValue102_g2 - ase_positionOS ) * step( 0.01 , ase_positionOS.y ) );
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_MainTex1_g1 = i.uv_texcoord;
			float4 tex2DNode1_g1 = tex2D( _MainTex, uv_MainTex1_g1 );
			o.Albedo = ( tex2DNode1_g1 * _Color ).rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
			o.Alpha = 1;
			clip( tex2DNode1_g1.a - _AlphaCutoff );
		}

		ENDCG
	}
	Fallback Off
	CustomEditor "Nicrom.LPVegetationTransparent_MaterialInspector"
}
/*ASEBEGIN
Version=19908
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;1;-288,0;Inherit;False;LPW - Vegetation - Main;0;;1;c10505b4e6adf42409a0d65b86d42d4e;0;0;4;COLOR;7;FLOAT;10;FLOAT;11;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2;0,464;Inherit;False;Property;_AlphaCutoff;Alpha Cutoff;5;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3;-304,288;Inherit;False;LPW - Vegetation - Wind;6;;2;c1bd97fdc47c03a4d97ca42aeb626543;0;0;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;32,0;Float;False;True;-1;3;Nicrom.LPVegetationTransparent_MaterialInspector;0;0;Standard;Nicrom/LPW/ASE/Low Poly Vegetation Transparent;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;0;False;;0;Custom;0.5;True;True;0;False;Opaque;;AlphaTest;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;True;_AlphaCutoff;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;0;0;1;7
WireConnection;0;3;1;10
WireConnection;0;4;1;11
WireConnection;0;10;1;0
WireConnection;0;11;3;0
ASEEND*/
//CHKSM=46171A38858A2938C91684D221CE2671EE407C44