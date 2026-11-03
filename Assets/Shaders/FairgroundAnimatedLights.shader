// Fairground/AnimatedLights — luces emisivas animadas en la GPU (I4.2 / I4.7). Sin iluminación (Unlit):
// la "luz" la pone el color HDR, que el Bloom convierte en brillo.
//
// Dos comportamientos que se mezclan con _ChaseAmount:
//   Chase   (bombillas): se encienden "en cadena" como en las marquesinas. Una de cada _ChaseGroup está
//                        apagada y ese hueco avanza una bombilla cada _ChaseStep segundos.
//   Twinkle (luciérnagas): cada pieza late a su ritmo y, opcionalmente, flota un poco (_DriftAmplitude).
//
// Datos por vértice (los escribe AnimatedMeshPostprocessor al importar el FBX):
//   R = índice de la bombilla a lo largo de la guirnalda / 255   (Chase)
//   G = valor aleatorio por pieza (desfase del Twinkle y de la deriva)
//
// Por qué un shader y no un script que cambie materiales: las 23 bombillas de la caseta son UNA malla.
// Para animarlas desde C# habría que partirla en 23 objetos (23 draw calls) o crear materiales por
// bloques. En el shader cada vértice calcula su propio encendido y todo sigue siendo un único dibujado.
Shader "Fairground/AnimatedLights"
{
    Properties
    {
        [HDR] _OnColor ("Color encendida (HDR)", Color) = (4, 3, 1.6, 1)
        _OffColor ("Color apagada", Color) = (0.25, 0.15, 0.05, 1)

        [Header(Chase)]
        _ChaseAmount ("Chase (1) / Twinkle (0)", Range(0, 1)) = 1
        _ChaseStep ("Segundos por paso", Float) = 0.25
        _ChaseGroup ("Una apagada cada N", Float) = 3

        [Header(Twinkle)]
        _TwinkleSpeed ("Latidos por segundo", Float) = 0.6
        _TwinkleMin ("Brillo mínimo", Range(0, 1)) = 0.15
        _DriftAmplitude ("Deriva (m)", Float) = 0
        _DriftSpeed ("Velocidad de la deriva", Float) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OnColor;
                half4 _OffColor;
                float _ChaseAmount;
                float _ChaseStep;
                float _ChaseGroup;
                float _TwinkleSpeed;
                float _TwinkleMin;
                float _DriftAmplitude;
                float _DriftSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half   intensity  : TEXCOORD0;   // 0 = apagada, 1 = encendida (igual en toda la pieza)
                half   fogFactor  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float t = _Time.y;
                float seed = v.color.g * 6.2831853;

                // Chase: índice entero de la bombilla + número de paso. fmod(...) == 0 → la apagada del grupo
                float index = round(v.color.r * 255.0);
                float step = floor(t / max(_ChaseStep, 0.01));
                float chase = fmod(index + step, max(_ChaseGroup, 2.0)) < 0.5 ? 0.0 : 1.0;

                // Twinkle: seno con desfase propio, elevado al cuadrado para que pase más tiempo apagada
                float s = 0.5 + 0.5 * sin(t * _TwinkleSpeed * 6.2831853 + seed);
                float twinkle = lerp(_TwinkleMin, 1.0, s * s);

                o.intensity = lerp(twinkle, chase, _ChaseAmount);

                // Deriva: toda la pieza comparte 'seed', así que se mueve entera (no se deforma)
                float3 drift = float3(sin(t * _DriftSpeed + seed * 3.0),
                                      sin(t * _DriftSpeed * 0.7 + seed * 5.0),
                                      cos(t * _DriftSpeed * 0.9 + seed * 2.0)) * _DriftAmplitude;

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz + drift);
                o.positionCS = pos.positionCS;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 color = lerp(_OffColor.rgb, _OnColor.rgb, i.intensity);
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Sin pase DepthOnly a propósito: las luciérnagas se desplazan en el vértice y un pase de
        // profundidad sin esa deriva las recortaría. Son piezas diminutas: no hacen falta en el depth texture.
    }

    FallBack Off
}
