#version 330 core

// Vintage Story fog cube vertex shader
// Renders fog volumes as cubes with vertical density gradient

uniform mat4 projectionMatrix;
uniform mat4 viewMatrix;
uniform mat4 modelMatrix;

// Cube position and scale uniforms
uniform vec3 cubeOrigin;     // World position of cube corner (min XYZ)
uniform vec3 cubeSize;       // Size of cube (typically 32, height, 32 for chunks)

// VS uses layout location 0 for vertex position
layout(location = 0) in vec3 vertexPositionIn;

out vec3 worldPos;           // World position for fragment shader
out float normalizedY;       // Y position normalized to 0-1 within fog range

uniform float fogMinY;
uniform float fogMaxY;

void main()
{
    // Transform unit cube to world position
    vec3 scaledPos = vertexPositionIn * cubeSize + cubeOrigin;
    worldPos = scaledPos;

    // Calculate normalized Y for density gradient (0 at bottom, 1 at top)
    normalizedY = clamp((scaledPos.y - fogMinY) / (fogMaxY - fogMinY), 0.0, 1.0);

    // Transform to clip space
    gl_Position = projectionMatrix * viewMatrix * vec4(scaledPos, 1.0);
}
