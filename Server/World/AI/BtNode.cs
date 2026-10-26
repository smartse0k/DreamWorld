namespace DreamWorld.World.AI;

/// <summary>
/// 상태를 가지지 않는 공유 Behavior Tree의 최상위 추상 노드.
/// </summary>
public abstract class BtNode<TContext>
{
    /// <summary>
    /// 전달된 컨텍스트를 기반으로 노드를 평가합니다.
    /// </summary>
    public abstract BtStatus Tick(TContext context);
}

/// <summary>
/// 자식 노드들을 순서대로 실행하며 하나라도 성공(Success)하거나 실행 중(Running)이면 즉시 반환하는 선택 노드.
/// </summary>
public class SelectorNode<TContext> : BtNode<TContext>
{
    private readonly IReadOnlyList<BtNode<TContext>> _children;

    public SelectorNode(params BtNode<TContext>[] children)
    {
        _children = children;
    }

    public override BtStatus Tick(TContext context)
    {
        for (int i = 0; i < _children.Count; i++)
        {
            BtStatus status = _children[i].Tick(context);
            if (status != BtStatus.Failure)
            {
                return status;
            }
        }

        return BtStatus.Failure;
    }
}

/// <summary>
/// 자식 노드들을 순서대로 실행하며 모두 성공(Success)해야 성공을 반환하는 순차 노드.
/// </summary>
public class SequenceNode<TContext> : BtNode<TContext>
{
    private readonly IReadOnlyList<BtNode<TContext>> _children;

    public SequenceNode(params BtNode<TContext>[] children)
    {
        _children = children;
    }

    public override BtStatus Tick(TContext context)
    {
        for (int i = 0; i < _children.Count; i++)
        {
            BtStatus status = _children[i].Tick(context);
            if (status != BtStatus.Success)
            {
                return status;
            }
        }

        return BtStatus.Success;
    }
}

/// <summary>
/// 조건식을 평가하여 Success 또는 Failure를 반환하는 조건 노드.
/// </summary>
public class ConditionNode<TContext> : BtNode<TContext>
{
    private readonly Func<TContext, bool> _predicate;

    public ConditionNode(Func<TContext, bool> predicate)
    {
        _predicate = predicate;
    }

    public override BtStatus Tick(TContext context)
    {
        return _predicate(context) ? BtStatus.Success : BtStatus.Failure;
    }
}

/// <summary>
/// 지정된 행동을 수행하고 결과 상태를 반환하는 액션 노드.
/// </summary>
public class ActionNode<TContext> : BtNode<TContext>
{
    private readonly Func<TContext, BtStatus> _action;

    public ActionNode(Func<TContext, BtStatus> action)
    {
        _action = action;
    }

    public override BtStatus Tick(TContext context)
    {
        return _action(context);
    }
}
