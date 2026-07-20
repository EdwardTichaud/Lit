namespace Lit.Performance
{
    /// <summary>
    /// Versioned hard limits shared by the Ice editor pipeline, validation and runtime.
    /// Values intentionally live in one place so generation and CI cannot drift apart.
    /// </summary>
    public static class IcePerformanceBudgetPolicy
    {
        public const int MaxGeneratedVertexCount = 150_000;
        public const long MaxGeneratedMeshBytes = 25L * 1024L * 1024L;
        public const int MaxLocalFlameInfluences = 4;

        public static bool IsGeneratedMeshWithinBudget(
            int generatedVertexCount,
            long generatedMeshBytes)
        {
            return generatedVertexCount >= 0
                && generatedVertexCount <= MaxGeneratedVertexCount
                && generatedMeshBytes >= 0L
                && generatedMeshBytes <= MaxGeneratedMeshBytes;
        }

        public static bool CanGenerateBarycentricMesh(long predictedVertexCount)
        {
            return predictedVertexCount >= 0L
                && predictedVertexCount <= MaxGeneratedVertexCount;
        }
    }
}
