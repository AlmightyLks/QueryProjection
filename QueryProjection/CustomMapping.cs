using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace QueryProjection;

public struct CustomMapping<TInput, TOutput> : IMapping<TInput>
{
    public required string To { get; set; }

    private Expression<Func<TInput, TOutput>> _fromExpression;

    [SetsRequiredMembers]
    public CustomMapping(string to, Expression<Func<TInput, TOutput>> from)
    {
        if (from is not LambdaExpression)
            throw new ArgumentException($"{nameof(from)} may only be a LambdaExpression");

        To = to;
        _fromExpression = from;
    }

    public Type GetResultType(ParameterExpression xParameter)
    {
        return _fromExpression.Body.Type;
    }

    public Expression BuildExpression(ParameterExpression xParameter)
    {
        var exp = (LambdaExpression)ParameterRebinder.ReplaceParameters(new()
        {
            { _fromExpression.Parameters.First(), xParameter }
        }, _fromExpression);

        return exp.Body;
    }
    sealed class ParameterRebinder : ExpressionVisitor
    {
        readonly Dictionary<ParameterExpression, ParameterExpression> map;

        ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> map)
        {
            this.map = map ?? [];
        }

        public static Expression ReplaceParameters(Dictionary<ParameterExpression, ParameterExpression> map, Expression exp)
        {
            return new ParameterRebinder(map).Visit(exp);
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            if (map.TryGetValue(p, out ParameterExpression? replacement))
            {
                p = replacement;
            }

            return base.VisitParameter(p);
        }
    }
}