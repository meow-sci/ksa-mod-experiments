#version 450
layout(location=0) in vec3 worldPosition;
layout(location=1) in vec3 worldNormal;
layout(location=2) in vec2 textureUv;
layout(location=0) out vec4 color;
layout(set=0,binding=0) uniform sampler2D diffuseMap;
layout(set=0,binding=1) uniform sampler2D normalMap;
layout(set=0,binding=2) uniform sampler2D pbrMap;
layout(set=0,binding=3) uniform sampler2D opacityMap;
layout(set=0,binding=4) uniform sampler2D thicknessMap;
layout(push_constant) uniform PreviewPush {
    mat4 viewProjection;
    vec4 camera;
    vec4 maps;
    vec4 options;
} pc;
void main() {
    if (pc.maps.w > .5 && texture(opacityMap, textureUv).r < .5) discard;
    vec4 sampleColor = pc.maps.x > .5 ? texture(diffuseMap, textureUv) : vec4(.62,.67,.73,0);
    // A neutral studio preview: source colors bypass clutter's terrain-color alpha convention.
    vec3 base = pc.options.y > .5 ? sampleColor.rgb : mix(pow(max(sampleColor.rgb,vec3(0)),vec3(2.2)),sampleColor.rgb,sampleColor.a);
    vec3 n = normalize(worldNormal);
    if (!gl_FrontFacing) n = -n;
    if (pc.maps.y > .5) {
        vec3 dp1=dFdx(worldPosition), dp2=dFdy(worldPosition);
        vec2 duv1=dFdx(textureUv), duv2=dFdy(textureUv);
        vec3 t=cross(dp2,n)*duv1.x+cross(n,dp1)*duv2.x;
        vec3 b=cross(dp2,n)*duv1.y+cross(n,dp1)*duv2.y;
        float norm=max(dot(t,t),dot(b,b));
        if (norm > 1e-12) n=normalize(mat3(t*inversesqrt(norm),b*inversesqrt(norm),n)*(texture(normalMap,textureUv).xyz*2-1));
    }
    vec3 pbr=pc.maps.z>.5?texture(pbrMap,textureUv).rgb:vec3(1,.65,0);
    vec3 v=normalize(pc.camera.xyz-worldPosition);
    vec3 light=normalize(vec3(.5,.85,.7));
    vec3 fill=normalize(vec3(-.8,.3,-.4));
    float diffuse=max(dot(n,light),0)*.75+max(dot(n,fill),0)*.2+.22;
    float spec=pow(max(dot(n,normalize(light+v)),0),mix(128,4,clamp(pbr.g,0,1)));
    vec3 result=base*diffuse*pbr.r+mix(vec3(.04),base,pbr.b)*spec*.7;
    if(pc.options.x>.5) result+=base*(1-texture(thicknessMap,textureUv).r)*max(dot(-n,light),0)*.25;
    color=vec4(clamp(result,0,1),1);
}
