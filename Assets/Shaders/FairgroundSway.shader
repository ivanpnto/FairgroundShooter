// Fairground/Sway — material iluminado (PBR de URP) con DESPLAZAMIENTO DE VÉRTICES (I4.2).
// Lo usan los banderines de la caseta (ondean hacia delante y atrás) y las algas del fondo marino
// (se balancean a los lados). La animación ocurre entera en la GPU: no hay ningún script por pieza.
//
// Cuánto se mueve cada vértice lo dice su COLOR DE VÉRTICE, que calcula AnimatedMeshPostprocessor al
// importar el FBX:
//   R = peso (0 = sujeto: el cordel del banderín o la base del alga; 1 = la punta, que se mueve del todo)
//   G = valor aleatorio por pieza, usado como desfase para que no se muevan todas a la vez
//
// Hay que desplazar igual el vértice en TODOS los pases (color, sombras y profundidad). Si la sombra
// usara la posición sin mover, la sombra del banderín se quedaría quieta mientras él ondea.
Shader "Fairground/Sway"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Suavidad", Range(0, 1)) = 0.3

        [Header(Movimiento)]
        _WaveDirection ("Dirección del desplazamiento (espacio objeto)", Vector) = (0, 0, 1, 0)
        _WaveAmplitude ("Amplitud (m)", Float) = 0.06
        _WaveFrequency ("Frecuencia (ciclos/s)", Float) = 1.2
        _WaveLength ("Longitud de onda a lo largo de X (m)", Float) = 3
        _FlutterAmount ("Aleteo secundario (0-1)", Range(0, 1)) = 0.35

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Todas las propiedades en UnityPerMaterial: así el material es compatible con el SRP Batcher
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            float4 _WaveDirection;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveLength;
            float _FlutterAmount;
            float _Cull;
        CBUFFER_END

        // Desplazamiento en espacio objeto. Onda principal que recorre la pieza a lo largo de X
        // (los banderines ondean "en ola" a lo largo del cordel) + un aleteo más rápido e irregular.
        float3 Sway(float3 positionOS, half4 color)
        {
            float weight = color.r;
            float phase = color.g * 6.2831853;
            float t = _Time.y * _WaveFrequency * 6.2831853;
            float along = positionOS.x / max(_WaveLength, 0.01) * 6.2831853;

            float wave = sin(t - along + phase * 0.3);
            float flutter = sin(t * 2.7 + phase) * sin(t * 1.3 + phase * 2.0);

            float amount = (wave + flutter * _FlutterAmount) * _WaveAmplitude * weight;
            return positionOS + normalize(_WaveDirection.xyz + 1e-5) * amount;
        }
        ENDHLSL

        // ---------- Pase principal: iluminación de URP (luz principal, focos, sombras, Forward+, niebla)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 posOS = Sway(v.positionOS.xyz, v.color);
                VertexPositionInputs pos = GetVertexPositionInputs(posOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                // La normal no se recalcula: en piezas planas y pequeñas el desplazamiento apenas la cambia
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float3 normalWS = normalize(i.normalWS);
                normalWS = frontFace ? normalWS : -normalWS;   // con Cull Off (algas), la cara trasera también se ilumina bien

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);   // luz ambiental de la escena (degradado azul de la noche)
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);   // necesario en Forward+
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb;
                surface.alpha = 1;
                surface.metallic = 0;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, i.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // ---------- Sombras: misma posición desplazada que el pase principal
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(Sway(v.positionOS.xyz, v.color));
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);   // focos: dirección distinta en cada vértice
            #else
                float3 lightDir = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------- Profundidad (pre-pase de profundidad, Forward+ y post-procesado)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(Sway(v.positionOS.xyz, v.color));
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
