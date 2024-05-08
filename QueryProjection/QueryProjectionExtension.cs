using System.Collections.ObjectModel;
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
