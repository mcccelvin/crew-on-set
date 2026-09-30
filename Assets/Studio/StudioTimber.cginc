#ifndef CREW_STUDIO_TIMBER
#define CREW_STUDIO_TIMBER
// World-space boards: vertical wall planks and long ceiling boards, no UV stretching.
float3 StudioTimber(float3 p, float3 normal, float3 color, float strength)
{
    float2 uv = abs(normal.y) > 0.7 ? p.xz : (abs(normal.x) > abs(normal.z) ? p.zy : p.xy);
    float width = 0.24;
    float board = floor(uv.x / width);
    float seed = frac(sin(board * 127.1) * 43758.5453);
    float across = frac(uv.x / width) * width;
    float along = frac((uv.y + seed * 2.8) / 2.8) * 2.8;
    float aa = max(max(fwidth(uv.x), fwidth(uv.y)), 0.0005);
    float edge = min(min(across, width-across), min(along, 2.8-along));
    float gap = 1-smoothstep(0.0015, 0.0015+aa, edge);
    float phase = uv.x*260 + sin(uv.y*2.1+seed*9)*2.5;
    float grain = sin(phase)*saturate(1-fwidth(phase)*0.4);
    float variation = (seed-0.5)*0.13 + grain*strength*0.25;
    return color * (1+variation) * (1-gap*0.4);
}
#endif
