Shader "Custom/Ocean"
{
    Properties
    {
        [Header(Color)]
        _DeepColor ("Deep Color", Color) = (0.02, 0.08, 0.15, 1)
        _CrestColor ("Crest Color", Color) = (0.1, 0.4, 0.45, 1)
        _CrestColorScale ("Crest Color Scale", Float) = 0.5

        [Header(Lighting)]
        _Smoothness ("Reflection Smoothness", Range(0, 1)) = 0.9
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
        _SpecularPower ("Specular Power", Range(8, 1024)) = 256
        _SpecularStrength ("Specular Strength", Range(0, 5)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.5

        // Directions come from the _OceanRandom global uploaded by OceanGrid
        [Header(Waves)]
        _Steepness ("Highest steepness", Range(0, 1)) = 0.8
        _Wavelength ("Highest wavelength", Float) = 20
        _SteepFactor ("Steepness factor", Float) = 0.82
        _WavelengthFactor ("Wavelength factor", Float) = 0.82
        [IntRange] _NumWaves ("Number of waves", Range(0, 32)) = 4
        _WaveDrag ("Wave drag", Range(-2, 2)) = 0.5
        // Waves shorter than this only affect the per-pixel normal, not the vertices
        _MinVertexWavelength ("Min vertex wavelength", Float) = 3

        [Header(Wind)]
        // 0 = waves travel along +X, 90 = along +Z
        _WindAngle ("Wind direction (degrees)", Range(0, 360)) = 0
        // Max deviation of each wave from the wind direction, either side
        _WindSpread ("Wind spread (degrees)", Range(0, 180)) = 60
        _Speed ("Wind speed", Float) = 1

        [Header(Detail Normals)]
        [Normal][NoScaleOffset] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailStrength ("Detail Strength", Range(0, 2)) = 0.5
        // Tile sizes in world units; non-multiples of each other hide the repetition
        _DetailTileA ("Detail Tile Size A", Float) = 7
        _DetailTileB ("Detail Tile Size B", Float) = 3.3
        _DetailScrollSpeed ("Detail Scroll Speed", Float) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;
            half4 _CrestColor;
            half _CrestColorScale;
            half _Smoothness;
            half _FresnelPower;
            half _SpecularPower;
            half _SpecularStrength;
            half _AmbientStrength;
            half _ReflectionStrength;
            half _Steepness;
            half _Wavelength;
            half _SteepFactor;
            half _WavelengthFactor;
            float _NumWaves;
            float _WaveDrag;
            float _MinVertexWavelength;
            float _WindAngle;
            float _WindSpread;
            float _Speed;
            half _DetailStrength;
            float _DetailTileA;
            float _DetailTileB;
            float _DetailScrollSpeed;
        CBUFFER_END

        float4 _OceanRandom[32];

        // Returns the summed displacement of a grid point p for all waves and accumulates
        // the surface's partial derivatives so the normal can be built analytically.
        // Waves shorter than minWavelength fade out of the displacement (not the derivatives)
        // so the mesh never has to represent detail finer than its grid.
        float3 GerstnerWave(float3 p, float minWavelength, inout float3 tangent, inout float3 binormal)
        {
            float3 displacement = 0;
            int numWaves = _NumWaves;
            // geometric series: s0 * (1 - r^n) / (1 - r) = _Steepness
            float steepness = _Steepness * (1 - _SteepFactor) / (1 - pow(_SteepFactor, numWaves));
            float wavelength = _Wavelength;
            // Where each wave is sampled; warped by the slopes of the waves before it
            float2 samplePos = p.xz;

            for (int i = 0; i < numWaves; i++)
            {
                float k = TWO_PI / wavelength;

                float c = sqrt(9.8 / k) * _Speed;
                // Random offset in [-spread, spread] around the wind direction
                float angle = radians(_WindAngle + (_OceanRandom[i].x * 2 - 1) * _WindSpread);
                float2 d = float2(cos(angle), sin(angle));
                float f = k * (dot(d, samplePos) - c * _Time.y);
                float a = steepness / k;
    
                float s, co;
                sincos(f, s, co);

                tangent += float3(-d.x * d.x * steepness * s, d.x * steepness * co, -d.x * d.y * steepness * s);
                binormal += float3(-d.x * d.y * steepness * s, d.y * steepness * co, -d.y * d.y * steepness * s);
                
                // Linear fade from minWavelength to twice that, to avoid popping
                float fade = saturate((wavelength - minWavelength) / max(minWavelength, 1e-4));
                displacement += float3(d.x * a * co, a * s, d.y * a * co) * fade;

                // (dh/dx, dh/dz) of this wave is d * steepness * cos(f); scaling by the
                // amplitude keeps the push proportional to the wave's size
                samplePos -= d * steepness * co * a * _WaveDrag;

                steepness *= _SteepFactor;
                wavelength *= _WavelengthFactor;
            }

            return displacement;
        }

        // Waves are evaluated in world space so the C# side can sample the same surface
        float3 DisplaceOcean(float3 gridWS)
        {
            float3 tangent = float3(1, 0, 0);
            float3 binormal = float3(0, 0, 1);
            return gridWS + GerstnerWave(gridWS, _MinVertexWavelength, tangent, binormal);
        }

        // Evaluated per pixel from the undisplaced grid position, so lighting includes every
        // wave and doesn't show the vertex grid. The unused displacement is compiled out.
        float3 OceanNormal(float3 gridWS)
        {
            float3 tangent = float3(1, 0, 0);
            float3 binormal = float3(0, 0, 1);
            GerstnerWave(gridWS, 0, tangent, binormal);
            return normalize(cross(binormal, tangent));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 gridWS : TEXCOORD1;
                float height : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 gridWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.gridWS = gridWS;
                OUT.positionWS = DisplaceOcean(gridWS);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.height = OUT.positionWS.y - gridWS.y;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            TEXTURE2D(_DetailNormalMap);
            SAMPLER(sampler_DetailNormalMap);

            // Two layers of the detail map, scrolling with the wind at different scales and
            // angles, whiteout-blended onto the wave normal. Mapped in world XZ, so the
            // tangent-space x/y of the map correspond to world X/Z.
            half3 ApplyDetailNormals(half3 n, float2 posXZ)
            {
                float windAngle = radians(_WindAngle);
                float2 windA = float2(cos(windAngle), sin(windAngle));
                float2 windB = float2(cos(windAngle + 0.7), sin(windAngle + 0.7));
                float t = _Time.y * _DetailScrollSpeed * _Speed;

                float2 uvA = posXZ / _DetailTileA - windA * t / _DetailTileA;
                float2 uvB = posXZ / _DetailTileB - windB * t * 0.7 / _DetailTileB;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, uvA), _DetailStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, uvB), _DetailStrength);
                half3 detail = normalize(half3(a.xy + b.xy, a.z * b.z));

                // Whiteout blend with the Y-up wave normal (detail z = world Y)
                return normalize(half3(n.x + detail.x, n.y * detail.z, n.z + detail.y));
            }

            half3 Shade(Light light, half3 albedo, half3 n, half3 v, inout half3 specular)
            {
                half3 radiance = light.color * light.distanceAttenuation * light.shadowAttenuation;
                half ndotl = saturate(dot(n, light.direction));
                half3 h = SafeNormalize(light.direction + v);
                specular += radiance * pow(saturate(dot(n, h)), _SpecularPower) * _SpecularStrength * ndotl;
                return radiance * albedo * ndotl;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 n = ApplyDetailNormals(OceanNormal(IN.gridWS), IN.gridWS.xz);
                half3 v = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half3 albedo = lerp(_DeepColor.rgb, _CrestColor.rgb, saturate(IN.height * _CrestColorScale + 0.5));

                half3 specular = 0;
                half3 color = SampleSH(n) * albedo * _AmbientStrength;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                color += Shade(mainLight, albedo, n, v, specular);

                #if defined(_ADDITIONAL_LIGHTS)
                // LIGHT_LOOP_BEGIN expects a variable named inputData in Forward+
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    color += Shade(light, albedo, n, v, specular);
                }
                #endif // USE_FORWARD_PLUS

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    color += Shade(light, albedo, n, v, specular);
                LIGHT_LOOP_END
                #endif // ADDITIONAL LIGHTS

                half3 reflection = GlossyEnvironmentReflection(reflect(-v, n), 1 - _Smoothness, _ReflectionStrength);
                half fresnel = 0.02 + 0.98 * pow(1 - saturate(dot(n, v)), _FresnelPower);
                color = lerp(color, reflection, fresnel) + specular;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Calculate displaced ocean vertices for camera depth prepass/depth texture
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                float3 positionWS = DisplaceOcean(TransformObjectToWorld(positionOS.xyz));
                return TransformWorldToHClip(positionWS);
            }

            half frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
