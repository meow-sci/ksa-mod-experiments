#version 450
layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;
layout(location=2) in vec2 uv;
layout(location=0) out vec3 worldPosition;
layout(location=1) out vec3 worldNormal;
layout(location=2) out vec2 textureUv;
layout(push_constant) uniform PreviewPush {
    mat4 viewProjection;
    vec4 camera;
    vec4 maps;
    vec4 options;
} pc;
void main() {
    worldPosition = position;
    worldNormal = normal;
    textureUv = uv;
    gl_Position = pc.viewProjection * vec4(position, 1);
}
