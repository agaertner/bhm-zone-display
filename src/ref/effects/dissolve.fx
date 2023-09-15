#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_2_0
#define PS_SHADERMODEL ps_2_0
#else
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_4_0
#endif

float Opacity = 1.0f;
float Amount = 0.0f;
bool Slide = false;
bool Glow = true;
float4 GlowColor = float4(1.0f, 0.5f, 0.0f, 1.0f);
float GlowRange = 0.04f;
float GlowFalloff = 0.05f;

sampler s0 = sampler_state {
    AddressU = Mirror;
    AddressV = Mirror;
};

// Used to create a pseudo dissolve mask at runtime
float GeneratePerlinNoise(float2 uv)
{
    // Initialize noise variables
    float noise = 0.0f;
    float frequency = 1.0f;
    float amplitude = 1.0f;
    float2 coord = uv * frequency;

    // Add multiple octaves
    for (int i = 0; i < 5; i++)
    {
        noise += (amplitude * tex2D(s0, coord).r);
        amplitude *= 0.5f;
        frequency *= 2.0f;
        coord = uv * frequency;
    }

    // Remap the noise value to [0, 1]
    noise = (noise + 2.5f) / 5.0f;

    return noise;
}

float4 Dissolve(float4 Position : SV_POSITION, float4 Color : COLOR0, float2 TexCoords : TEXCOORD0) : COLOR0 {
    if (Amount == 1.0f) {
        discard;
    }
    
    float4 sampledColor = tex2D(s0, TexCoords) * Color;
    
    if (sampledColor.a == 0.0f) {
        discard;
    }
    
    if (Amount == 0) {
        return sampledColor * Opacity;
    }
    
    float dissolve_value;
    
    if (Slide) {
        // Diagonal slide effect
        float diagonal = TexCoords.x + TexCoords.y;
        dissolve_value = diagonal - Amount;
    } else {
        // Generate perlin noise for the dissolve value
        dissolve_value = GeneratePerlinNoise(TexCoords);
    }
    
    float isVisible = dissolve_value - Amount;
    clip(isVisible);
    
    if (Glow) {
      float isGlowing = smoothstep(GlowRange + GlowFalloff, GlowRange, isVisible);
      float4 glow = isGlowing * GlowColor;
      sampledColor += glow;
    }
    
    return sampledColor * Opacity;
}

technique
{
    pass icon
    {
        PixelShader = compile PS_SHADERMODEL Dissolve();
    }
}
