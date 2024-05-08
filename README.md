# QueryProjection

---

## NuGet
https://www.nuget.org/packages/QueryProjection

---

# Why was this library created?
We needed a way to query only what was needed from the frontend, as our previous property filtering was done during json serialization, and resulted in unnecessarily long queries and big result sets as well as long-running queries. However, we did not want to introduce something more time-consuming and complex like GraphQL.  
This library allows projecting upon an EFCore database query to only select what is being asked for, without statically typing out the properties, but also offering lambda expressions for specific use cases.

# How to use
This library essentially offers 1 primary extension method which is the following:
```cs
public static class QueryProjectionExtension
{
    public static IQueryable<object> Project<T>(this IQueryable<T> query, List<IMapping<T>> mappings, ParameterExpression? xParameter = null);
}
```

As you can see there is `IMapping<T>`. There is 2 structs implementing that interface. 
`FromToMapping` and `CustomMapping`.

`FromToMapping` allows to simply map from the source object's property to a field name of your choice which will be in the resulting object.  
`CustomMapping` allows you to specify a lambda expression, and acts as a Func<TInput, TOutput>. This allows to also include additional more complex ways to add fields to the result object.  

## Usage
Create a list of mapping that you would like to apply.  
For example simply a to-from mapping with two strings.  
A to-from mapping where the from string is nested within the object.  
Or a more complex to-from mapping where from is a Func.  
```cs
var fromToMapping = new List<IMapping<Person>>()
{
    new FromToMapping<Person>(to: "PersonId" , from: "Id"),
    new FromToMapping<Person>(to: "FirstName" , from: "IdCard.FirstName"),
    new CustomMapping<Person, bool>(to: "HasJohnOnIdCard", from: x => x.IdCard.FirstName.Contains(FirstName))
};

IQueryable<object> query = _context.People.Project(fromToMapping);
List<object> results = query.ToList();
```

## What does the projection do?
It essentially dynamically creates anonymous objects within the assembly at runtime (like the compiler does at compile time) based on the each mapping's `To` Name and `From` Type.  
After doing that, it essentially generates an Expression<Func<T1, T2>> with something similar to an object initializer like from the above example, and calls `query.Select(expression)`.  
Thus:  
```cs
var query = _context.People.Project(fromToMapping);
// Identical to
var query = _ context.People.Select(x => new
{
  PersonId = x.Id,
  FirstName = x.IdCard.FirstName,
  HasJohnOnIdCard = x.IdCard.FirstName.Contains(FirstName)
});
```
