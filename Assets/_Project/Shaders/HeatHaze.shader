// Bends the finished frame like hot air. Strongest at the screen's edges, and nothing in the middle or around
// the bottom center where the pawn stands, so the pawn and the warning cells around it stay sharp.
Shader "ChessBoss/HeatHaze"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "HeatHaze"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            // How far a pixel is pushed, as a fraction of the screen.
            float _HazeOffset;
            float _HazeScale;
            float _HazeSpeed;
            // How far from the center the haze begins, 0 to 1.
            float _HazeInner;
            // The radius left clear around the bottom center, as a fraction of the screen.
            float _HazeClear;
            // Real time from the game, so slow motion doesn't still the air.
            float _HazeTime;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 fromCenter = abs(uv - 0.5) * 2;
                float edge = smoothstep(_HazeInner, 1, max(fromCenter.x, fromCenter.y))
                    * smoothstep(0, _HazeClear, distance(uv, float2(0.5, 0)));
                float2 wave = float2(
                    sin(uv.y * _HazeScale + _HazeTime * _HazeSpeed),
                    cos(uv.x * _HazeScale * 0.7 + _HazeTime * _HazeSpeed * 1.3));
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + wave * (_HazeOffset * edge));
            }
            ENDHLSL
        }
    }
}
