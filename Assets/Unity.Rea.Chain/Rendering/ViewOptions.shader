// The override ViewRenderFeature redraws the scene with while F1-F3 change how it is drawn (UNI-T56): an unlit
// sprite shader (URP's Sprite-Unlit-Default, without its debug and skinning variants) whose texture and cull state
// follow Root.vb's flags. _Textured 0 is F2's "Use Shaded": each sprite, line and quad in its own colour, opaque, with
// no texture. _Cull is F3: Off, or Back to cull back faces. Wireframe (F1) is the device's state, set by the pass.
Shader "Hidden/MindAttic/Chain/ViewOptions"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Textured ("Textured", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull [_Cull]
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "ViewOptions"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Textured;
                float _Cull;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                // A flipped sprite is mirrored here, as URP's sprite shaders do, so it turns its back to the camera.
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                if (_Textured > 0.5)
                {
                    return CommonUnlitFragment(input, input.color);
                }

                // Untextured: the quad's own colour, opaque, as D3D draws a quad with no texture set.
                return half4(input.color.rgb, 1);
            }
            ENDHLSL
        }
    }
}
