using System.Linq.Expressions;

namespace QueryProjection;

public static class QueryProjectionExtension
{
    public static IQueryable<object> Project<T>(this IQueryable<T> query, Dictionary<string, string> fromToMappings, ParameterExpression? param = null)
    {
        var projection = BuildProjectExpression(query, fromToMappings, param);
        return query.Select(projection);
    }

    public static Expression<Func<T, object>> BuildProjectExpression<T>(IQueryable<T> query, Dictionary<string, string> fromToMappings, ParameterExpression? param = null)
    {
        return BuildProjectExpression<T>(fromToMappings, param);
    }

    public static Expression<Func<T, object>> BuildProjectExpression<T>(Dictionary<string, string> fromToMappings, ParameterExpression? param = null)
    {
        param ??= Expression.Parameter(typeof(T), "x");

        var anonType = GetAnonymousType(fromToMappings, param);

        var memberAssignments = anonType
            .GetFields()
            .Select(fi => Expression.Bind(fi, FilterExpressionProvider.NestedProperty(param, fromToMappings[fi.Name].Split('.'))))
            .ToList();

        var memberInitializations = Expression.MemberInit(Expression.New(anonType), memberAssignments);
        var funcExpression = Expression.Lambda<Func<T, object>>(memberInitializations, param);

        return funcExpression;
    }

    private static Type GetAnonymousType(Dictionary<string, string> fromToMappings, ParameterExpression xParameter)
    {
        var objectProperties = new Dictionary<string, Type>();
        foreach (var fromToMapping in fromToMappings)
        {
            var anonPropertyName = fromToMapping.Key;
            var anonPropertyType = FilterExpressionProvider.NestedProperty(xParameter, fromToMapping.Value.Split('.')).Type;
            objectProperties.Add(anonPropertyName, anonPropertyType);
        }
        var anonType = AnonymousTypeGenerator.FindOrCreateAnonymousType(objectProperties);

        return anonType;
    }
}
