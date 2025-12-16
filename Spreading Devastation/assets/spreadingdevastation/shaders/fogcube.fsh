#version 330 core

// Vintage Story fog cube fragment shader
// Creates volumetric fog with vertical density gradient (denser at bottom)

in vec3 worldPos;
in float normalizedY;

out vec4 fragColor;

// Fog parameters
uniform vec3 fogColor;           // RGB fog color
uniform float bottomDensity;     // Density at bottom (0-1)
uniform float topDensity;        // Density at top (0-1)
uniform float fogMinY;
uniform float fogMaxY;

// Camera/view info for depth-based blending
uniform vec3 cameraPos;
uniform float viewDistance;

void main()
{
    // Calculate fog density based on Y position (linear interpolation)
    // Bottom is denser, top is lighter
    float density = mix(bottomDensity, topDensity, normalizedY);

    // Distance-based fade (fog fades with distance from camera)
    float distToCamera = length(worldPos - cameraPos);
    float distFade = 1.0 - clamp(distToCamera / viewDistance, 0.0, 1.0);
    distFade = distFade * distFade; // Quadratic falloff for smoother transition

    // Final alpha combines density and distance fade
    float alpha = density * distFade;

    // Clamp alpha to reasonable range
    alpha = clamp(alpha, 0.0, 0.85);

    // Output fog color with calculated alpha
    fragColor = vec4(fogColor, alpha);
}
