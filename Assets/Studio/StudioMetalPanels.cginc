#ifndef CREW_STUDIO_METAL_PANELS
#define CREW_STUDIO_METAL_PANELS
#include "StudioTimber.cginc"
// World-scale panels keep their proportions on differently sized wall meshes.
void StudioMetalPanels(float3 position, float3 normal, inout float3 albedo,
    inout float metallic, inout float smoothness)
{
    float2 plane = abs(normal.x) > abs(normal.z) ? position.zy : position.xy;
    float2 size = float2(1.5, 2.5);
    float2 cell = frac(plane / size) * size;
    float2 border = min(cell, size - cell);
    float aa = max(max(fwidth(plane.x), fwidth(plane.y)), 0.0005);
    float seam = 1 - smoothstep(0.002, 0.002 + aa, min(border.x, border.y));
    albedo *= 1 - seam * 0.32;
    // Four inset screw heads per panel, with a recessed cross and a lit rim.
    float2 screw = border - 0.055;
    float radius = length(screw);
    float visibility = saturate(0.022 / aa);
    float head = (1 - smoothstep(0.012, 0.014 + aa, radius)) * visibility;
    float rim = smoothstep(0.009, 0.012, radius);
    float slot = (1 - smoothstep(0.001, 0.001 + aa, min(abs(screw.x), abs(screw.y))))
        * (1 - smoothstep(0.007, 0.008 + aa, radius));
    float3 steel = lerp(float3(0.32,0.34,0.36), float3(0.62,0.64,0.66), rim);
    steel *= 1 - slot * 0.8;
    albedo = lerp(albedo, steel, head);
    metallic = lerp(metallic, 0.85, head);
    smoothness = lerp(smoothness, 0.58, head);
}
#endif
