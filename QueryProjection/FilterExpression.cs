namespace QueryProjection;

public sealed class FilterExpression
{
    public string Property { get; set; } = null!;
    public string Value { get; set; } = null!;
    public FilterOperator Operator { get; set; }
}
