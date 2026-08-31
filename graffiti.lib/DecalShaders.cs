namespace MeowSci.GraffitiLib;

/// <summary>
/// The GLSL for the projected-decal pass, compiled at runtime with shaderc against KSA's own
/// shader include directory (so <c>Common/Camera.glsl</c> / <c>Common/TextureSet.glsl</c>
/// resolve to the shipped headers, whatever the game build).
/// </summary>
internal static class DecalShaders
{
    /// <summary>
    /// The push block, spelled identically in both stages (Vulkan requires the layouts to match).
    /// Each vec4 is one COLUMN of the row-vector 3×4 matrix — see <c>DecalRenderer.DecalPush</c>.
    /// </summary>
    private const string PushBlock =
        """
        layout(push_constant) uniform Decal {
            vec4 d2e0; vec4 d2e1; vec4 d2e2;
            vec4 e2d0; vec4 e2d1; vec4 e2d2;
            uint texId;
            float alpha;
            float brightness;
            float normalCutoff;
        } dc;
        """;

    /// <summary>
    /// Transforms the unit cube's corners into ego and projects them. Nothing is interpolated:
    /// the fragment shader works entirely from the depth buffer and the push constants.
    /// </summary>
    internal const string Vertex =
        $$"""
          #version 450

          #include "Common/Camera.glsl"

          layout(location = 0) in vec3 inPos;

          {{PushBlock}}

          void main()
          {
              // Row-vector 3x4: each d2e row is a COLUMN of DecalToEgo, so this is float3.Transform.
              vec4 p = vec4(inPos, 1.0);
              vec3 ego = vec3(dot(dc.d2e0, p), dot(dc.d2e1, p), dot(dc.d2e2, p));
              gl_Position = global.camera.viewProjection * vec4(ego, 1.0);
          }
          """;

    /// <summary>
    /// Reconstructs the scene position under the pixel from the resolved reverse-Z depth, rejects
    /// it if it falls outside the decal box or the surface faces the wrong way, and shades the
    /// sampled texel with a single sun term plus planetshine.
    /// </summary>
    internal const string Fragment =
        $$"""
          #version 450

          // Must precede the include: TextureSet.glsl declares globalTextures[]/samplers[] at this set.
          #define SET_TEXTURE 2
          #include "Common/TextureSet.glsl"
          #include "Common/Camera.glsl"

          layout(set = 1, binding = 0) uniform sampler2D sceneDepth;

          {{PushBlock}}

          layout(location = 0) out vec4 outColor;

          // The fast 2.2 approximation from Common/Shared.glsl, inlined rather than including
          // Shared.glsl, which would pull in four more files for one pow().
          vec3 DecalGammaToLinear(vec3 sRGBValue)
          {
              return pow(sRGBValue, vec3(2.2));
          }

          void main()
          {
              // Screen-sized and single-sample after ResolveAttachments: exactly one texel per fragment.
              ivec2 size = textureSize(sceneDepth, 0);
              float z = texelFetch(sceneDepth, ivec2(gl_FragCoord.xy), 0).r;

              // Same convention as Camera.ScreenToEgoNearPlane: ndc = 2*p/size - 1 on BOTH axes,
              // no Y flip -- the reverse-Z projection already carries it.
              vec2 ndc = (gl_FragCoord.xy / vec2(size)) * 2.0 - 1.0;
              vec4 v = global.camera.inverseProjection * vec4(ndc, z, 1.0);
              v /= v.w;

              // The view matrix is rotation-only, so undoing it lands in ego.
              vec3 pEgo = (global.camera.inverseView * vec4(v.xyz, 1.0)).xyz;

              // The receiving surface's normal, from the reconstructed position's screen
              // derivatives. Taken BEFORE any discard: derivatives are only defined in uniform
              // control flow. (Noisy at depth discontinuities -- that is the known one-pixel edge
              // artifact of a projected decal, and the NaN-safe tests below eat it.)
              vec3 n = normalize(cross(dFdx(pEgo), dFdy(pEgo)));

              // Reverse-Z: 0 is the far plane AND what untouched background reads as. A decal has
              // nothing to stick to there.
              if (z <= 0.0) discard;

              vec4 p4 = vec4(pEgo, 1.0);
              vec3 pDec = vec3(dot(dc.e2d0, p4), dot(dc.e2d1, p4), dot(dc.e2d2, p4));
              // Negated form so a NaN coordinate (a degenerate reconstruction) discards too.
              if (!all(lessThanEqual(abs(pDec), vec3(0.5)))) discard;

              // Decal +z in ego = row 2 of the row-vector matrix = the z of each packed column.
              vec3 axisZ = normalize(vec3(dc.d2e0.z, dc.d2e1.z, dc.d2e2.z));

              // The winding the derivatives produce is arbitrary, so orient the normal towards
              // the decal instead of trusting its sign.
              float facing = dot(n, axisZ);
              if (facing < 0.0) { n = -n; facing = -facing; }
              // Negated again: a NaN normal must discard, not sail through as "not less than".
              if (!(facing >= dc.normalCutoff)) discard;

              // Debug: an 8x8 magenta checker in decal space proves the box, the reverse-Z
              // reconstruction and the NDC convention without involving any art.
              if (dc.texId == 0xFFFFFFFFu)
              {
                  vec2 cell = floor(pDec.xy * 8.0);
                  float checker = mod(cell.x + cell.y, 2.0);
                  outColor = vec4(1.0, 0.0, 1.0, 0.35 + 0.3 * checker);
                  return;
              }

              // Sampler 0 is the bindless table's linear-clamped, full-mip sampler. PNG row 0 is
              // the TOP, so v is flipped to keep decal +y pointing at the top of the image.
              vec4 texel = SAMPLE_TEXTURE(dc.texId, 0, pDec.xy * vec2(1.0, -1.0) + 0.5);
              if (texel.a < 0.004) discard;

              // sunPosition is the sun's EGO position and sunColor the star's light colour.
              // planetColor is the nearby atmospheric body's lit colour and is ZERO for an
              // airless body or a camera in shadow, so the small constant keeps a night-side
              // decal from going black.
              vec3 L = normalize(global.lighting.sunPosition.xyz - pEgo);
              vec3 ambient = 0.12 * global.lighting.planetColor.rgb + vec3(0.02);
              vec3 lit = DecalGammaToLinear(texel.rgb)
                  * (global.lighting.sunColor.rgb * max(dot(n, L), 0.0) + ambient)
                  * dc.brightness;

              outColor = vec4(lit,
                  texel.a * dc.alpha * smoothstep(dc.normalCutoff, dc.normalCutoff + 0.2, facing));
          }
          """;
}
