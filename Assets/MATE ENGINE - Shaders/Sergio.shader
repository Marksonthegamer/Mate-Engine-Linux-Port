Shader "UI/SimpleMask"
{
    SubShader
    {
        // Standard-UI RenderQueue
        Tags { "Queue"="Overlay-1" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }

        // Kein Farboutput, aber Depth schreiben
        ColorMask 0
        ZWrite On
        ZTest Always
        Cull Off
        Lighting Off

        Pass {}
    }
}
