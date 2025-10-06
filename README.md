# UserQueriesSharp

[![NuGet Version](https://img.shields.io/nuget/v/UserQueriesSharp)](https://www.nuget.org/packages/UserQueriesSharp/)

A powerful search system that allows users to select and order by any property of an IQueryable.

User Queries are designed to work with the Entity Framework system. Just pass in the DbSet as the IQueryable and add the neccessary attributes to the entity class.

[See this](https://github.com/AidenBradley24/UserQueriesSharp/blob/master/UserQueries/UserQueries.md) for a thorough explaination of UserQueries.

## Quick Start

1. Add the NuGet package.
2. Add the `UserQueryable` attribute to any property on your class you wish the user to be able to filter / sort by.
3. Add the `PrimaryUserQueryable` attribute to your class for the default search property (usually the name or title)
4. Instantiate a `UserQueryProvider` or use the extension method `EvaluateUserQuery` and pass in the user's query string to query the results.
---
## Examples

```csharp
[PrimaryUserQueryable(nameof(Name))]
class ExampleModel
{
	public int Id { get; set; }

	[UserQueryable("name")]
	public string Name { get; set; } = "";

	[UserQueryable("intval")]
	public int IntValue { get; set; } = 0;

	[UserQueryable("floatval")]
	public float FloatValue { get; set; } = 0.0f;

	[UserQueryable("doubleval")]
	public double DoubleValue { get; set; } = 0.0;

	[UserQueryable("timespanval")]
	public TimeSpan TimeSpanValue { get; set; } = TimeSpan.FromSeconds(1);
}
```

`
Jim
`
-> returns any entity with the text "Jim" consecutively inside the name property. (case insensitive)

`
name * "Jim"
`
-> returns any entity with the text "Jim" consecutively inside the name property. (case insensitive)

`
intval < 10 & name = 'fred'
`
-> returns any entity with an intvalue less than 10 AND its "name" property is exactly "fred" (case insensitive)

`
timespanval < '01:00:00' orderby timespanval
`
-> returns any entity with a timespanvalue less than an hour, ordered ascending by its timespanvalue.

`
floatval: -100 - 100
`
-> returns any entity with floatvalue between -100 and 100 inclusive

---

## Example Use Cases


#### Extension method
Better for one-time situations.
```csharp
using Microsoft.EntityFrameworkCore;
using UserQueries;

public class Example
{
	DbSet<ExampleModel> dbSet; // get from your dbcontext

	public void MyMethod(string userInput) 
	{
		IQueryable<ExampleModel> result = dbSet.EvaluateUserQuery(userInput);
		foreach(var entity in result) 
		{
			Console.Log(entity.Name);
		}
	}
}
```

#### Provider method
Better for situations where queries are called multiple times on one set of data.
```csharp
using Microsoft.EntityFrameworkCore;
using UserQueries;

public class Example
{
	DbSet<ExampleModel> dbSet; // get from your dbcontext
	UserQueryProvider<ExampleModel> queryProvider;

	public Example() 
	{
		queryProvider = new UserQueryProvider<ExampleModel>(dbSet);
	}

	public void MyMethod(string userInput) 
	{
		foreach(var entity in queryProvider.EvaluateUserQuery(userInput)) 
		{
			Console.Log(entity.Name);
		}
	}
}
```