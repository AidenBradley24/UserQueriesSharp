namespace UserQueries.Tests
{
	public class UserQueryExtensionsTests
	{
		readonly ITestOutputHelper _output;
		public UserQueryExtensionsTests(ITestOutputHelper output)
		{
			_output = output;
		}


		[Theory]
		[InlineData("http://example.com/api", "name = 'test'", "name", true, 10, 1, "http://example.com/api/?query=name%20%3D%20%27test%27%20orderbydescending%20name&pageSize=10&currentPage=1")]
		[InlineData("http://example.com/api/", "intvalue > 5", null, false, 20, 2, "http://example.com/api/?query=intvalue%20%3E%205&pageSize=20&currentPage=2")]
		public void ConstructRequest_BasicCases(string baseURL, string userFilter, string? orderBy, bool descending, int pageSize, int currentPage, string expectedURL)
		{
			var result = UserQueryExtensions.ConstructRequest(baseURL, userFilter, orderBy, descending, pageSize, currentPage);
			Assert.Equal(expectedURL, result);
		}

		[Fact]
		public void Test_GetQueryableProperties()
		{
			var properties = UserQueryExtensions.GetQueryableProperties(typeof(TestModel), out var modelExpression);

			var result = properties
				.Cast<object>()
				.Select(static item =>
				{
					var itemType = item.GetType();

					var queryName =
						itemType.GetProperty("QueryName")?.GetValue(item) as string ??
						itemType.GetProperty("Key")?.GetValue(item) as string ??
						throw new Xunit.Sdk.XunitException("Unable to read query name from returned item.");

					var propertyValue =
						itemType.GetProperty("Property")?.GetValue(item) ??
						itemType.GetProperty("Value")?.GetValue(item) ??
						throw new Xunit.Sdk.XunitException("Unable to read property from returned item.");

					var propertyName = propertyValue is System.Reflection.PropertyInfo propertyInfo
						? propertyInfo.Name
						: propertyValue.GetType().GetProperty("Name")?.GetValue(propertyValue) as string
							?? throw new Xunit.Sdk.XunitException("Unable to read property name from returned property.");

					return $"Query Name: {queryName}, Property: {propertyName}";
				})
				.ToArray();

			foreach (var line in result)
			{
				_output.WriteLine(line);
			}

			Assert.Equal(
			[
				"Query Name: name, Property: Name",
				"Query Name: intvalue, Property: IntValue",
				"Query Name: floatvalue, Property: FloatValue",
				"Query Name: doublevalue, Property: DoubleValue",		
				"Query Name: timespanvalue, Property: TimeSpanValue",
				"Query Name: embedded, Property: EmbeddedValue",
				"Query Name: default, Property: Name"
			],
			result);
		}
	}
}
