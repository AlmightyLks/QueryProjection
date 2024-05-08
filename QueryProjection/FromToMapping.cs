using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace QueryProjection;

public struct FromToMapping<T> : IMapping<T>
{
    public required string From { get; set; }
    public required string To { get; set; }

    [SetsRequiredMembers]
    public FromToMapping(string to, string from)
    {
        To = to;
        From = from;
    }

    public Type GetResultType(ParameterExpression xParameter)
    {
        return BuildExpression(xParameter).Type;
    }
    public Expression BuildExpression(ParameterExpression xParameter)
    {
        return NestedProperty(xParameter, From.Split('.'));
    }
    private static MemberExpression NestedProperty(Expression propertyHolder, string[] propertyPath)
    {
        return (MemberExpression)propertyPath.Aggregate(propertyHolder, Expression.Property);
    }
}
