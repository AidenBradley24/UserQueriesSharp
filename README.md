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