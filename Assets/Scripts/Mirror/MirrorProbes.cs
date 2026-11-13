using UnityEngine;
using UnityEngine.Rendering;

// Baked lighting in the mirror worlds. Lightmapped surfaces only need their lightmap slot (Instantiate drops it),
// their lightmap UVs come with the mesh.
// Probe-lit renderers (avatar, doors, props without lightmap UVs) would sample the probes behind the mirror,
// where there are none, so each copy gets the probe lighting of its original instead, mirrored.
public static class MirrorProbes
{
    // Coefficients odd in the mirror axis change sign. Unity's order: 1, y, z, x, xy, yz, 3z²-1, xz, x²-y².
    static readonly int[][] Odd = { new[] { 3, 4, 7 }, new[] { 1, 4, 5 }, new[] { 2, 5, 7 } }; // x, y, z
    static readonly SphericalHarmonicsL2[] one = new SphericalHarmonicsL2[1];
    static MaterialPropertyBlock block;

    public static bool Baked => LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0;

    public static bool UsesProbes(Renderer r) =>
        r.lightProbeUsage == LightProbeUsage.BlendProbes && (r.lightmapIndex < 0 || r.lightmapIndex >= 0xFFFE);

    // Reflection through a plane with the given normal (one of the world axes, the rooms are axis-aligned).
    public static SphericalHarmonicsL2 Mirror(SphericalHarmonicsL2 sh, Vector3 normal)
    {
        var n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        int axis = n.x >= n.y && n.x >= n.z ? 0 : n.y >= n.z ? 1 : 2;
        foreach (int c in Odd[axis])
            for (int channel = 0; channel < 3; channel++)
                sh[channel, c] = -sh[channel, c];
        return sh;
    }

    public static void Apply(Renderer source, Renderer copy, Vector3 normal)
    {
        LightProbes.GetInterpolatedProbe(source.bounds.center, source, out var sh);
        one[0] = Mirror(sh, normal);
        block ??= new MaterialPropertyBlock();
        copy.GetPropertyBlock(block);
        block.CopySHCoefficientArraysFrom(one);
        copy.SetPropertyBlock(block);
        copy.lightProbeUsage = LightProbeUsage.CustomProvided;
    }

    // Gives the copies the lightmap slots of their originals and the mirrored probe lighting.
    public static void CopyBaked(Transform source, Transform copy, Vector3 normal)
    {
        var a = source.GetComponentsInChildren<Renderer>(true);
        var b = copy.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            b[i].lightmapIndex = a[i].lightmapIndex;
            b[i].lightmapScaleOffset = a[i].lightmapScaleOffset;
            if (UsesProbes(a[i]))
                Apply(a[i], b[i], normal);
        }
    }

    // Pairs of probe-lit renderers in an original and its copy (same hierarchy, so the same order).
    public static (Renderer, Renderer)[] Pairs(Transform source, Transform copy)
    {
        var a = source.GetComponentsInChildren<Renderer>(true);
        var b = copy.GetComponentsInChildren<Renderer>(true);
        var pairs = new System.Collections.Generic.List<(Renderer, Renderer)>();
        for (int i = 0; i < a.Length && i < b.Length; i++)
            if (UsesProbes(a[i]))
                pairs.Add((a[i], b[i]));
        return pairs.ToArray();
    }
}
