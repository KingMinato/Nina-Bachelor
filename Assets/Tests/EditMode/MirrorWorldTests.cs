using NUnit.Framework;
using UnityEngine;

public class MirrorWorldTests
{
    GameObject worldGo, source, target;

    // Mirror plane at x = -3 facing into the left wall, like the left mirror of Room 1.
    [SetUp]
    public void SetUp()
    {
        worldGo = new GameObject("MirrorWorld");
        worldGo.transform.SetPositionAndRotation(new Vector3(-3, 0, 0), Quaternion.Euler(0, -90, 0));
        worldGo.transform.localScale = new Vector3(1, 1, -1);
        worldGo.AddComponent<MirrorWorld>();
        source = new GameObject("Source");
        target = new GameObject("Target");
        target.transform.SetParent(worldGo.transform, false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(worldGo);
        Object.DestroyImmediate(source);
    }

    [Test]
    public void Place_ReflectsPositionThroughPlane()
    {
        source.transform.position = new Vector3(1, 1.6f, 2);
        worldGo.GetComponent<MirrorWorld>().Place(source.transform, target.transform);
        AssertNear(new Vector3(-7, 1.6f, 2), target.transform.position);
    }

    [Test]
    public void Place_ReflectsFacing()
    {
        // Facing the mirror (-x): the reflection faces back towards the player (+x).
        source.transform.rotation = Quaternion.Euler(0, -90, 0);
        worldGo.GetComponent<MirrorWorld>().Place(source.transform, target.transform);
        AssertNear(Vector3.right, RenderedForward());

        // Facing along the mirror (+z): the reflection faces the same way.
        source.transform.rotation = Quaternion.identity;
        worldGo.GetComponent<MirrorWorld>().Place(source.transform, target.transform);
        AssertNear(Vector3.forward, RenderedForward());
    }

    // transform.forward ignores the parent's negative scale, the render matrix does not.
    Vector3 RenderedForward() => target.transform.localToWorldMatrix.MultiplyVector(Vector3.forward).normalized;

    // Probe lighting of a mirror copy: light arriving from direction d at the original arrives from the reflected
    // direction at the copy, for mirrors facing x and z.
    [Test]
    public void MirroredProbeLighting_IsReflected()
    {
        var sh = new UnityEngine.Rendering.SphericalHarmonicsL2();
        sh.AddAmbientLight(new Color(0.2f, 0.2f, 0.2f));
        sh.AddDirectionalLight(new Vector3(0.6f, -0.5f, 0.62f).normalized, new Color(1f, 0.8f, 0.6f), 1f);
        var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.forward, new Vector3(0.5f, 0.3f, -0.8f).normalized };
        foreach (var normal in new[] { Vector3.right, Vector3.forward })
        {
            var mirrored = MirrorProbes.Mirror(sh, normal);
            var reflected = System.Array.ConvertAll(dirs, d => Vector3.Reflect(d, normal));
            var a = new Color[dirs.Length];
            var b = new Color[dirs.Length];
            sh.Evaluate(dirs, a);
            mirrored.Evaluate(reflected, b);
            for (int i = 0; i < dirs.Length; i++)
                Assert.Less(Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].b - b[i].b), 0.001f, $"normal {normal}, dir {dirs[i]}");
        }
    }

    static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.Less(Vector3.Distance(expected, actual), 0.001f, $"expected {expected}, got {actual}");
    }
}
