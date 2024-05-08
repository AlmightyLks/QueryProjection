using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace QueryProjection;

public static class QueryProjectionExtension
{
    public static IQueryable<object> Project<T>(this IQueryable<T> query, List<IMapping<T>> mappings, ParameterExpression? xParameter = null)
    {
        var projection = BuildProjectExpression(query, mappings, xParameter);
        return query.Select(projection);
    }

    public static Expression<Func<T, object>> BuildProjectExpression<T>(IQueryable<T> query, List<IMapping<T>> mappings, ParameterExpression? xParameter = null)
    {
        return BuildProjectExpression<T>(mappings, xParameter);
    }

    public static Expression<Func<T, object>> BuildProjectExpression<T>(List<IMapping<T>> mappings, ParameterExpression? xParameter = null)
    {
        xParameter ??= Expression.Parameter(typeof(T), "x");

        var anonType = GetAnonymousType(mappings, xParameter);

        var expressions = new List<Expression>();
        foreach (var fieldInfo in anonType.GetFields())
        {
            if (mappings.Find(x => x.To == fieldInfo.Name) is { } mapping)
            {
                expressions.Add(mapping.BuildExpression(xParameter));
            }
        }

        var ctor = anonType.GetConstructors()[0];

        var memberInitializations = Expression.New(ctor, expressions, anonType.GetFields());
        var funcExpression = Expression.Lambda<Func<T, object>>(memberInitializations, xParameter);

        return funcExpression;
    }

    private static Type GetAnonymousType<T>(List<IMapping<T>> mappings, ParameterExpression xParameter)
    {
        var objectProperties = new Dictionary<string, Type>();
        foreach (var fromToMapping in mappings)
        {
            var anonPropertyName = fromToMapping.To;
            var anonPropertyType = fromToMapping.GetResultType(xParameter);
            objectProperties.Add(anonPropertyName, anonPropertyType);
        }
        var anonType = AnonymousTypeGenerator.FindOrCreateAnonymousType(objectProperties);

        return anonType;
    }
}

public interface IMapping<T>
{
    string To { get; set; }
    Type GetResultType(ParameterExpression xParameter);
    Expression BuildExpression(ParameterExpression xParameter);
}

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


public struct CustomMapping<TInput, TOutput> : IMapping<TInput>
{
    public required string To { get; set; }

    private Expression<Func<TInput, TOutput>> _fromExpression;

    [SetsRequiredMembers]
    public CustomMapping(string to, Expression<Func<TInput, TOutput>> fromExpression)
    {
        if (fromExpression is not LambdaExpression)
            throw new ArgumentException($"{nameof(fromExpression)} may only be a LambdaExpression");

        To = to;
        _fromExpression = fromExpression;
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