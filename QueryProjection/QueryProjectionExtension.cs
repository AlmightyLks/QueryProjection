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
    MemberAssignment BuildMemberAssignment(FieldInfo fieldInfo, ParameterExpression xParameter);
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
        return Map(xParameter).Type;
    }

    public MemberAssignment BuildMemberAssignment(FieldInfo fieldInfo, ParameterExpression xParameter)
    {
        var memberExpression = Map(xParameter);
        return Expression.Bind(fieldInfo, memberExpression);
    }

    private MemberExpression Map(ParameterExpression xParameter)
    {
        return FilterExpressionProvider.NestedProperty(xParameter, From.Split('.'));
    }
}


public struct CustomMapping<TInput, TOutput> : IMapping<TInput>
{
    public required string To { get; set; }
    private Expression<Func<TInput, TOutput>> _fromExpression;

    [SetsRequiredMembers]
    public CustomMapping(string to, Expression<Func<TInput, TOutput>> fromExpression)
    {
        To = to;
        _fromExpression = fromExpression;
    }

    public Type GetResultType(ParameterExpression xParameter)
    {
        return typeof(TOutput);
    }

    public MemberAssignment BuildMemberAssignment(FieldInfo fieldInfo, ParameterExpression xParameter)
    {
        return Expression.Bind(fieldInfo, _fromExpression.Body);
    }
}
