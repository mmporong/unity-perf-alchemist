using System.Threading;
using System.Threading.Tasks;

namespace UnityPerformanceAlchemist.Editor
{
    /// <summary>LLM 프로바이더 공통 인터페이스 (계약 §5).</summary>
    public interface ILlmProvider
    {
        Task<string> CompleteAsync(string prompt, CancellationToken ct);
        string DisplayName { get; }
    }
}
