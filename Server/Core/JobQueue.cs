using System.Collections.Concurrent;

namespace DreamWorld.Server.Core;

/// <summary>
/// 작업을 모아두었다가 순차적으로 일괄 실행하는 작업 큐.
/// </summary>
public class JobQueue
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public int Count => _queue.Count;

    /// <summary>
    /// 실행할 작업을 큐에 추가합니다.
    /// </summary>
    public void Enqueue(Action job)
    {
        _queue.Enqueue(job);
    }

    /// <summary>
    /// 큐에 대기 중인 모든 작업을 순서대로 실행합니다.
    /// </summary>
    public void Flush()
    {
        while (_queue.TryDequeue(out Action? job))
        {
            try
            {
                job.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JobQueue] Error while executing job: {ex}");
            }
        }
    }
}
