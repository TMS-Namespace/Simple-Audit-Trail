// modified from https://github.com/lytico/db4o/tree/master/db4o.net/Db4objects.Db4o.Linq/Db4objects.Db4o.Linq/Expressions

/* Copyright (C) 2007 - 2008  Versant Inc.  http://www.db4o.com */

using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Help.ExpressionComparison;

internal sealed class ExpressionComparison : ExpressionVisitor
{
    #region Vars

    private readonly Queue<Expression> _candidates;
    private Expression? _candidate;

    #endregion

    #region Public

    public bool AreEqual { get; private set; } = true;

    public ExpressionComparison(Expression a, Expression b)
    {
        _candidates = new Queue<Expression>(new ExpressionEnumeration(b));

        Visit(a);

        if (_candidates.Count > 0)
        {
            Stop();
        }
    }

    #endregion

    #region Protected

    protected override void Visit(Expression expression)
    {
        if (expression == null)
            return;

        if (!AreEqual)
            return;

        _candidate = PeekCandidate();

        if (!CheckNotNull(_candidate))
            return;

        if (!CheckAreOfSameType(_candidate!, expression))
            return;

        PopCandidate();

        base.Visit(expression);
    }

    protected override void VisitConstant(ConstantExpression constant)
    {
        var candidate = CandidateFor(constant);

        if (!CheckEqual(constant.Value, candidate?.Value))
        {
            return;
        }
    }

    protected override void VisitMemberAccess(MemberExpression member)
    {
        var candidate = CandidateFor(member);

        if (!CheckEqual(member.Member, candidate?.Member))
        {
            return;
        }

        base.VisitMemberAccess(member);
    }

    protected override void VisitMethodCall(MethodCallExpression methodCall)
    {
        var candidate = CandidateFor(methodCall);

        if (!CheckEqual(methodCall.Method, candidate?.Method))
        {
            return;
        }

        base.VisitMethodCall(methodCall);
    }

    protected override void VisitParameter(ParameterExpression parameter)
    {
        var candidate = CandidateFor(parameter);

        if (!CheckEqual(parameter.Name, candidate?.Name))
        {
            return;
        }
    }

    protected override void VisitTypeIs(TypeBinaryExpression type)
    {
        var candidate = CandidateFor(type);

        if (!CheckEqual(type.TypeOperand, candidate?.TypeOperand))
        {
            return;
        }

        base.VisitTypeIs(type);
    }

    protected override void VisitBinary(BinaryExpression binary)
    {
        var candidate = CandidateFor(binary);

        if (!CheckEqual(binary.Method, candidate?.Method))
        {
            return;
        }

        if (!CheckEqual(binary.IsLifted, candidate?.IsLifted))
        {
            return;
        }

        if (!CheckEqual(binary.IsLiftedToNull, candidate?.IsLiftedToNull))
        {
            return;
        }

        base.VisitBinary(binary);
    }

    protected override void VisitUnary(UnaryExpression unary)
    {
        var candidate = CandidateFor(unary);

        if (!CheckEqual(unary.Method, candidate?.Method))
        {
            return;
        }

        if (!CheckEqual(unary.IsLifted, candidate?.IsLifted))
        {
            return;
        }

        if (!CheckEqual(unary.IsLiftedToNull, candidate?.IsLiftedToNull))
        {
            return;
        }

        base.VisitUnary(unary);
    }

    protected override void VisitNew(NewExpression newExpression)
    {
        var candidate = CandidateFor(newExpression);

        if (!CheckEqual(newExpression.Constructor, candidate?.Constructor))
        {
            return;
        }

        CompareList(newExpression?.Members, candidate?.Members);

        base.VisitNew(newExpression);
    }

    #endregion

    #region Private

    private Expression? PeekCandidate()
    {
        if (_candidates.Count == 0)
        {
            return null;
        }

        return _candidates.Peek();
    }

    private Expression PopCandidate() => _candidates.Dequeue();

    private bool CheckAreOfSameType(Expression candidate, Expression expression)
    {
        if (!CheckEqual(expression.NodeType, candidate.NodeType))
        {
            return false;
        }

        if (!CheckEqual(expression.Type, candidate.Type))
        {
            return false;
        }

        return true;
    }

    private void Stop() => AreEqual = false;

    private T? CandidateFor<T>(T? original) where T : Expression => (T?)_candidate;

    private void CompareList<T>(ReadOnlyCollection<T>? collection, ReadOnlyCollection<T>? candidates)
        => CompareList(collection, candidates, EqualityComparer<T>.Default.Equals);

    private void CompareList<T>(ReadOnlyCollection<T>? collection, ReadOnlyCollection<T>? candidates, Func<T, T, bool> comparer)
    {
        if (!CheckAreOfSameSize(collection, candidates))
        {
            return;
        }



        for (var i = 0; i < collection.Count; i++)
        {
            if (!comparer(collection[i], candidates[i]))
            {
                Stop();
                return;
            }
        }
    }

    private bool CheckAreOfSameSize<T>(ReadOnlyCollection<T>? collection, ReadOnlyCollection<T>? candidate)
        => CheckEqual(collection?.Count, candidate?.Count);

    private bool CheckNotNull<T>(T? t) where T : class
    {
        if (t == null)
        {
            Stop();
            return false;
        }

        return true;
    }

    private bool CheckEqual<T>(T? t, T? candidate)
    {
        if (!EqualityComparer<T>.Default.Equals(t, candidate))
        {
            Stop();
            return false;
        }

        return true;
    }

#endregion

}