using System.Linq.Expressions;

namespace QueryProjection;

public interface IMapping<T>
{
    string To { get; set; }
    Type GetResultType(ParameterExpression xParameter);
    Expression BuildExpression(ParameterExpression xParameter);
}
