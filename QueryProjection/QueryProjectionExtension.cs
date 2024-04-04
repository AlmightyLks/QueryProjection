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

        var memberAssignments = new List<MemberAssignment>();
        foreach (var fieldInfo in anonType.GetFields())
        {
            var mapping = mappings.Find(x => x.To == fieldInfo.Name);
            if (mapping is { })
            {
                memberAssignments.Add(mapping.BuildMemberAssignment(fieldInfo, xParameter));
            }
        }

        var memberInitializations = Expression.MemberInit(Expression.New(anonType), memberAssignments);
        var funcExpression = Expression.Lambda<Func<T, object>>(memberInitializations, xParameter);

        return funcExpression;
    }

    private static Type GetAnonymousType<T>(List<IMapping<T>> mappings, ParameterExpression xParameter)
    {
        var objectProperties = new Dictionary<string, Type>();
        foreach (var fromToMapping in mappings)
        {
            var anonPropertyName = fromToMapping.To;
            var anonPropertyType = FilterExpressionProvider.NestedProperty(xParameter, fromToMapping.From.Split('.')).Type;
            objectProperties.Add(anonPropertyName, anonPropertyType);
        }
        var anonType = AnonymousTypeGenerator.FindOrCreateAnonymousType(objectProperties);

        return anonType;
    }
}

public interface IMapping<T>
{
    string From { get; set; }
    string To { get; set; }
    MemberAssignment BuildMemberAssignment(FieldInfo fieldInfo, ParameterExpression xParameter);
}

public struct FromToMapping<T> : IMapping<T>
{
    public string From { get; set; }
    public string To { get; set; }

    public MemberAssignment BuildMemberAssignment(FieldInfo fieldInfo, ParameterExpression xParameter)
    {
        return Expression.Bind(fieldInfo, FilterExpressionProvider.NestedProperty(xParameter, From.Split('.')));
    }
}

public struct CustomMapping<T> : IMapping<T>
{
    public string From { get; set; }
    public string To { get; set; }

    public MemberAssignment BuildMemberAssignment(FieldInfo fieldInfo, ParameterExpression xParameter)
    {
        throw new NotImplementedException();
    }
}
