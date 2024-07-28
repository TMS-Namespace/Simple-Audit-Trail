/* Copyright (C) 2007 - 2008  Versant Inc.  http://www.db4o.com */
// modified from https://github.com/lytico/db4o/tree/master/db4o.net/Db4objects.Db4o.Linq/Db4objects.Db4o.Linq/Expressions

using System.Collections;
using System.Linq.Expressions;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Help.ExpressionComparison;

internal sealed class ExpressionEnumeration : ExpressionVisitor, IEnumerable<Expression>
{
    private readonly List<Expression> _expressions = [];

    public ExpressionEnumeration(Expression expression) => Visit(expression);

    protected override void Visit(Expression expression)
    {
        if (expression == null)
        {
            return;
        }

        _expressions.Add(expression);
        base.Visit(expression);
    }

    public IEnumerator<Expression> GetEnumerator() => _expressions.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
