using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace QueryProjection;

public static class FilterExpressionProvider
{
    private static readonly MethodInfo StringEqualsMethod = new Func<string, bool>("".Equals).Method;
    private static readonly MethodInfo StringContainsMethod = new Func<string, bool>("".Contains).Method;
    private static readonly MethodInfo StringStartsWithMethod = new Func<string, bool>("".StartsWith).Method;
    private static readonly MethodInfo StringEndsWithMethod = new Func<string, bool>("".EndsWith).Method;
    private static readonly MethodInfo ArrayContainsMethod = typeof(Enumerable).GetMethods().Where(x => x.Name == nameof(Enumerable.Contains)).Single(x => x.GetParameters().Length == 2).MakeGenericMethod(typeof(string));
    private static readonly MethodInfo ToStringMethod = typeof(object).GetMethod("ToString")!;
    public static Expression<Func<T, bool>> FilterToExpression<T>(string property, string filter)
    {
        var segments = new List<string>();
        var sb = new StringBuilder();
        bool insideString = false;
        for (int i = 0; i < filter.Length; i++)
        {
            char c = filter[i];
            if (!insideString && Char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c == '"')
            {
                insideString = !insideString;
                continue;
            }

            if (!insideString && c is 'o' or 'O' && i + 1 < filter.Length)
            {
                char nextChar = filter[i + 1];
                if (nextChar is 'r' or 'R')
                {
                    segments.Add(sb.ToString());
                    sb.Clear();
                    i++;
                    continue;
                }
            }

            sb.Append(c);
        }

        if (sb.Length > 0)
        {
            segments.Add(sb.ToString());
        }

        var expressions = new List<Expression>(segments.Count);
        var param = Expression.Parameter(typeof(T), "x");
        var prop = Expression.Property(param, property);

        foreach (string segment in segments)
        {
            bool startsWith = segment.StartsWith('%');
            bool endsWith = segment.EndsWith('%');

            string value;
            MethodInfo method;
            if (startsWith && endsWith)
            {
                method = StringContainsMethod;
                value = segment.Substring(1, segment.Length - 2);
            }
            else if (startsWith)
            {
                method = StringStartsWithMethod;
                value = segment.Substring(1, segment.Length - 1);
            }
            else if (endsWith)
            {
                method = StringEndsWithMethod;
                value = segment.Substring(0, segment.Length - 1);
            }
            else
            {
                method = StringEqualsMethod;
                value = segment;
            }

            expressions.Add(Expression.Call(prop, method, Expression.Constant(value)));
        }

        if (expressions.Count == 0)
        {
            return x => true;
        }

        var body = expressions.Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    public static MemberExpression NestedProperty(Expression propertyHolder, string[] propertyPath)
    {
        return (MemberExpression)propertyPath.Aggregate(propertyHolder, Expression.Property);
    }

    public static Expression GetExpression(FilterExpression filter, ParameterExpression objectParams)
    {
        var propertyAccessor = NestedProperty(objectParams, filter.Property.Split('.'));

        if (filter.Operator == FilterOperator.In)
        {
            var values = filter.Value.Split(',');
            var propertyType = ((PropertyInfo)propertyAccessor.Member).PropertyType;
            if (propertyType == typeof(Guid))
            {
                if (values.Length == 0)
                {
                    return Expression.Constant(false);
                }
                return values.Select(x => Expression.Equal(Expression.Constant(new Guid(x)), propertyAccessor)).Aggregate(Expression.OrElse);
            }
            else if (propertyType.IsEnum)
            {
                var enumValues = Array.CreateInstance(propertyType, values.Length);
                for (int i = 0; i < enumValues.Length; i++)
                {
                    var trimmedValue = values[i].AsSpan().Trim();
                    enumValues.SetValue(Int32.TryParse(trimmedValue, out int parsed) ? Enum.ToObject(propertyType, parsed) : Enum.Parse(propertyType, trimmedValue), i);
                }
                var methodInfo = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).Where(m => m.Name == nameof(Enumerable.Contains)).Single(m => m.GetParameters().Length == 2).MakeGenericMethod(propertyType);
                return Expression.Call(methodInfo, Expression.Constant(enumValues), propertyAccessor);
            }

            return Expression.Call(ArrayContainsMethod, Expression.Constant(values), propertyAccessor);
        }

        var constant = GetConstantExpression(filter.Value, propertyAccessor);

        if (filter.Operator == FilterOperator.Equal)
        {
            return Expression.Equal(propertyAccessor, constant);
        }
        if (filter.Operator == FilterOperator.NotEqual)
        {
            return Expression.NotEqual(propertyAccessor, constant);
        }
        if (filter.Operator == FilterOperator.GreaterThan)
        {
            return Expression.GreaterThan(propertyAccessor, constant);
        }
        if (filter.Operator == FilterOperator.GreaterThanOrEqual)
        {
            return Expression.GreaterThanOrEqual(propertyAccessor, constant);
        }
        if (filter.Operator == FilterOperator.LessThan)
        {
            return Expression.LessThan(propertyAccessor, constant);
        }
        if (filter.Operator == FilterOperator.LessThanOrEqual)
        {
            return Expression.LessThanOrEqual(propertyAccessor, constant);
        }
        if (filter.Operator == FilterOperator.StartsWith)
        {
            if (constant.Type != typeof(string))
            {
                return Expression.Call(Expression.Call(propertyAccessor, ToStringMethod), StringStartsWithMethod, Expression.Call(constant, ToStringMethod));
            }

            return Expression.Call(propertyAccessor, StringStartsWithMethod, constant);
        }
        if (filter.Operator == FilterOperator.Contains)
        {
            if (constant.Type != typeof(string))
            {
                return Expression.Call(Expression.Call(propertyAccessor, ToStringMethod), StringContainsMethod, Expression.Call(constant, ToStringMethod));
            }

            return Expression.Call(propertyAccessor, StringContainsMethod, constant);
        }

        if (filter.Operator == FilterOperator.IsEmpty)
        {
            if (constant.Type == typeof(object))
            {
                return Expression.OrElse(Expression.Equal(propertyAccessor, Expression.Constant(null)), Expression.Equal(propertyAccessor, Expression.Constant("")));
            }

            return Expression.Equal(propertyAccessor, constant);        //constant ist hier converted null irgendwas
        }
        if (filter.Operator == FilterOperator.IsNotEmpty)
        {
            if (constant.Type == typeof(object))
            {
                return Expression.Not(Expression.OrElse(Expression.Equal(propertyAccessor, Expression.Constant(null)), Expression.Equal(propertyAccessor, Expression.Constant(""))));
            }

            return Expression.Not(Expression.Equal(propertyAccessor, constant));
        }
        throw new NotImplementedException(filter.Operator.ToString());
    }

    public static Expression GetConstantExpression(string? stringValue, MemberExpression propertyAccessor)
    {
        var propertyType = ((PropertyInfo)propertyAccessor.Member).PropertyType;
        object? value = stringValue != null ? TypeDescriptor.GetConverter(propertyType).ConvertFromString(stringValue) : null;
        Expression constant = Expression.Constant(value);
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            constant = Expression.Convert(constant, propertyType);
        }
        return constant;
    }
}

