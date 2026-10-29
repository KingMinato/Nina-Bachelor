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

    static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.Less(Vector3.Distance(expected, actual), 0.001f, $"expected {expected}, got {actual}");
    }
}
