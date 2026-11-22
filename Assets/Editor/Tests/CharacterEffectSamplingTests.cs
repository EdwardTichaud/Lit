#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class CharacterEffectSamplingTests
{
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void CompiledEffectReadsSurfaceCoordinatesInsteadOfOneConstantVertex(int index)
    {
        var path=CharacterEffectSamplingValidator.GraphPaths[index];
        Assert.That(CharacterEffectSamplingValidator.ConsumesSurfaceSamples(CharacterEffectSamplingValidator.GeneratedSources(path)),Is.True,path);
    }
    [Test] public void OldCollapsedShaderCannotPassTheContract()
        =>Assert.That(CharacterEffectSamplingValidator.ConsumesSurfaceSamples(new[]{"float3 p=SampleMeshFloat3(buffer,uniformOffset,format);"}),Is.False);
}
#endif
