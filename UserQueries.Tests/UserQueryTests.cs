namespace UserQueries.Tests
{
	public class UserQueryTests
	{
		private static readonly IEnumerable<TestModel> root =
		[
			new TestModel() { Name = "Item1", IntValue = 1, FloatValue = 0f, DoubleValue = 0.0, TimeSpanValue = TimeSpan.FromSeconds(0) },
			new TestModel() { Name = "Item2", IntValue = 5, FloatValue = 5f, DoubleValue = 5.0, TimeSpanValue = TimeSpan.FromSeconds(30) },
			new TestModel() { Name = "Item3", IntValue = 10, FloatValue = 10f, DoubleValue = 10.0, TimeSpanValue = TimeSpan.FromSeconds(120) },
		];

		private static readonly IEnumerable<TestModel> extendedRoot = root.Concat(
		[
			new TestModel() { Name = "Item4", IntValue = 20, FloatValue = 7.5f, DoubleValue = 7.5, TimeSpanValue = TimeSpan.FromSeconds(240), Embedded = new EmbededModel() { EmbeddedValue = "Embedded1" } },
			new TestModel() { Name = "Item5", IntValue = -10, FloatValue = 7.5f, DoubleValue = -7.5, TimeSpanValue = TimeSpan.FromSeconds(240), Embedded = new EmbededModel() { EmbeddedValue = "Embedded2" } },
		]);

		private readonly ITestOutputHelper output;

		public UserQueryTests(ITestOutputHelper output)
		{
			this.output = output;
		}

		private static UserQueryProvider<TestModel> GetProvider()
		{
			return new UserQueryProvider<TestModel>(root.AsQueryable());
		}

		private static UserQueryProvider<TestModel> GetExtendedProvider()
		{
			return new UserQueryProvider<TestModel>(extendedRoot.AsQueryable());
		}

		[Theory]
		[InlineData("intvalue = 1", new[] { "Item1" })]
		[InlineData("intvalue != 5", new[] { "Item1", "Item3" })]
		[InlineData("intvalue < 5", new[] { "Item1" })]
		[InlineData("intvalue <= 5", new[] { "Item1", "Item2" })]
		[InlineData("intvalue > 1", new[] { "Item2", "Item3" })]
		[InlineData("intvalue >= 10", new[] { "Item3" })]
		[InlineData("floatvalue = 0.0", new[] { "Item1" })]
		[InlineData("floatvalue > 0.0", new[] { "Item2", "Item3" })]
		[InlineData("floatvalue < 10.0", new[] { "Item1", "Item2" })]
		[InlineData("doublevalue = 5.0", new[] { "Item2" })]
		[InlineData("doublevalue >= 10.0", new[] { "Item3" })]
		[InlineData("doublevalue <= 0.0", new[] { "Item1" })]
		[InlineData("timespanvalue = '00:00:30'", new[] { "Item2" })]
		[InlineData("timespanvalue > '00:00:00'", new[] { "Item2", "Item3" })]
		[InlineData("timespanvalue < '00:02:00'", new[] { "Item1", "Item2" })]
		[InlineData("name = 'Item1'", new[] { "Item1" })]
		[InlineData("name != 'Item1'", new[] { "Item2", "Item3" })]
		[InlineData("name ^ 'Item'", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("name !^ 'Item3'", new[] { "Item1", "Item2" })]
		[InlineData("name * 'em2'", new[] { "Item2" })]
		[InlineData("name !* '3'", new[] { "Item1", "Item2" })]
		public void Operator_Tests(string query, string[] expectedNames)
		{
			var provider = GetProvider();
			var result = provider.EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("intvalue=1", new[] { "Item1" })]
		[InlineData("intvalue!=5", new[] { "Item1", "Item3" })]
		[InlineData("intvalue<5", new[] { "Item1" })]
		[InlineData("intvalue<=5", new[] { "Item1", "Item2" })]
		[InlineData("intvalue>1", new[] { "Item2", "Item3" })]
		[InlineData("intvalue>=10", new[] { "Item3" })]
		[InlineData("floatvalue=0.0", new[] { "Item1" })]
		[InlineData("floatvalue>0.0", new[] { "Item2", "Item3" })]
		[InlineData("floatvalue<10.0", new[] { "Item1", "Item2" })]
		[InlineData("doublevalue=5.0", new[] { "Item2" })]
		[InlineData("doublevalue>=10.0", new[] { "Item3" })]
		[InlineData("doublevalue<=0.0", new[] { "Item1" })]
		[InlineData("timespanvalue='00:00:30'", new[] { "Item2" })]
		[InlineData("timespanvalue>'00:00:00'", new[] { "Item2", "Item3" })]
		[InlineData("timespanvalue<'00:02:00'", new[] { "Item1", "Item2" })]
		[InlineData("name='Item1'", new[] { "Item1" })]
		[InlineData("name!='Item1'", new[] { "Item2", "Item3" })]
		[InlineData("name^'Item'", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("name!^'Item3'", new[] { "Item1", "Item2" })]
		[InlineData("name*'em2'", new[] { "Item2" })]
		[InlineData("name!*'3'", new[] { "Item1", "Item2" })]
		public void Operator_Adjacent_Tests(string query, string[] expectedNames)
		{
			var provider = GetProvider();
			var result = provider.EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("intvalue = 1 & name = 'Item1'", new[] { "Item1" })]
		[InlineData("intvalue > 1 & intvalue < 10", new[] { "Item2" })]
		[InlineData("name * 'Item' & intvalue >= 5", new[] { "Item2", "Item3" })]
		[InlineData("floatvalue = 5.0 & doublevalue = 5.0", new[] { "Item2" })]
		[InlineData("timespanvalue > '00:00:00' & timespanvalue < '00:02:00'", new[] { "Item2" })]
		[InlineData("intvalue = 1 & intvalue = 5", new string[] { })]
		public void AndOperator_Tests(string query, string[] expectedNames)
		{
			var result = GetProvider().EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("intvalue = 1, intvalue = 5", new[] { "Item1", "Item2" })]
		[InlineData("name = 'Item2', name = 'Item3'", new[] { "Item2", "Item3" })]
		[InlineData("intvalue = 1, intvalue = 5 & name = 'Item2'", new[] { "Item1", "Item2" })]
		[InlineData("intvalue = 1, intvalue = 5 & name = 'Item3'", new[] { "Item1" })]
		[InlineData("floatvalue = 0.0, floatvalue = 10.0", new[] { "Item1", "Item3" })]
		[InlineData("doublevalue = 5.0, doublevalue = 10.0", new[] { "Item2", "Item3" })]
		[InlineData("timespanvalue = '00:00:00', timespanvalue = '00:02:00'", new[] { "Item1", "Item3" })]
		public void OrOperator_Tests(string query, string[] expectedNames)
		{
			var result = GetProvider().EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("orderby intvalue", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("orderby floatvalue", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("orderby doublevalue", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("orderby timespanvalue", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("orderby name", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("intvalue > 5 orderby timespanvalue", new[] { "Item3" })]
		[InlineData("intvalue > 1 orderby name", new[] { "Item2", "Item3" })]
		[InlineData("orderbydescending intvalue", new[] { "Item3", "Item2", "Item1" })]
		[InlineData("intvalue > 1 orderbydescending name", new[] { "Item3", "Item2" })]
		public void OrderBy_Tests(string query, string[] expectedNames)
		{
			var result = GetProvider().EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames, actualNames);
		}

		public static IEnumerable<object[]> InvalidQueries =>
		[
			["intvalue =="],
			["name ^"],
			["name = "],
			["intvalue ^ 10"],
			["name > 'z'"],
			["floatvalue ^ 1.0"],
			["doublevalue * 2.0"],
			["timespanvalue = 'notatimespan'"]
		];

		[Theory]
		[MemberData(nameof(InvalidQueries))]
		public void Invalid_Queries_Throw(string query)
		{
			var provider = GetProvider();
			var ex = Assert.Throws<InvalidUserQueryException>(() => provider.EvaluateUserQuery(query).ToList());
			output.WriteLine(ex.Message);
		}

		[Theory]
		[InlineData("1", new[] { "Item1" })]
		[InlineData("Item", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("a", new string[0])]
		[InlineData("", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("Item orderby name", new[] { "Item1", "Item2", "Item3" })]
		[InlineData("Item orderbydescending name", new[] { "Item3", "Item2", "Item1" })]
		public void Literal_Queries(string query, string[] expectedNames)
		{
			var provider = GetProvider();
			var result = provider.EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("name: ='Item1'", new[] { "Item1" })]
		[InlineData("intvalue: 10", new[] { "Item3" })]
		[InlineData("floatvalue: <=6", new[] { "Item1", "Item2" })]
		[InlineData("intvalue: 1, intvalue: 10", new[] { "Item1", "Item3" })]
		public void Refer_Queries(string query, string[] expectedNames)
		{
			var provider = GetProvider();
			var result = provider.EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("intvalue: 5-10", new[] { "Item2", "Item3" })]
		[InlineData("floatvalue: 6 - 9", new[] { "Item4", "Item5" })]
		[InlineData("intvalue: -10-5", new[] { "Item1", "Item2", "Item5" })]
		public void RangeOperator_Queries(string query, string[] expectedNames)
		{
			var provider = GetExtendedProvider();
			var result = provider.EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("intvalue < 0", new[] { "Item5" })]
		[InlineData("intvalue = -10", new[] { "Item5" })]
		[InlineData("intvalue =-10", new[] { "Item5" })]
		public void NegativeNumber_Queries(string query, string[] expectedNames)
		{
			var provider = GetExtendedProvider();
			var result = provider.EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Theory]
		[InlineData("embedded: 'Embedded1'", new[] { "Item4" })]
		[InlineData("embedded: 'Embedded2'", new[] { "Item5" })]
		[InlineData("embedded: 'Embedded'", new[] { "Item4", "Item5" })]
		[InlineData("embedded = 'Embedded1'", new[] { "Item4" })]
		[InlineData("embedded ^ 'Embedded'", new[] { "Item4", "Item5" })]
		[InlineData("embedded * '1'", new[] { "Item4" })]
		[InlineData("embedded !* 'Embedded'", new[] { "Item1", "Item2", "Item3" })]
		public void EmbeddedProperty_Queries(string query, string[] expectedNames)
		{
			var provider = GetExtendedProvider();
			var result = provider.EvaluateUserQuery(query);
			var actualNames = result.Select(i => i.Name).ToArray();
			Assert.Equal(expectedNames.OrderBy(n => n), actualNames.OrderBy(n => n));
		}

		[Fact]
		public void BuildEmbeddedPropertyAccess_MultipleLayers_StringPath_ReturnsTerminalPropertyAndValue()
		{
			var (property, accessor) = BuildEmbeddedAccessor<DeepEmbeddedRoot, string>(
				nameof(DeepEmbeddedRoot.Level1),
				nameof(DeepLevel1.Level2),
				nameof(DeepLevel2.StringLeaf),
				nameof(DeepStringLeaf.Text));

			var model = new DeepEmbeddedRoot
			{
				Level1 = new DeepLevel1
				{
					Level2 = new DeepLevel2
					{
						StringLeaf = new DeepStringLeaf
						{
							Text = "NestedValue"
						}
					}
				}
			};

			Assert.Equal(nameof(DeepStringLeaf.Text), property.Name);
			Assert.Equal("NestedValue", accessor(model));
		}

		[Fact]
		public void BuildEmbeddedPropertyAccess_MultipleLayers_StringPath_ReturnsEmptyStringWhenAnyNavigationIsNull()
		{
			var (_, accessor) = BuildEmbeddedAccessor<DeepEmbeddedRoot, string>(
				nameof(DeepEmbeddedRoot.Level1),
				nameof(DeepLevel1.Level2),
				nameof(DeepLevel2.StringLeaf),
				nameof(DeepStringLeaf.Text));

			var results = new[]
			{
				accessor(new DeepEmbeddedRoot { Level1 = null }),
				accessor(new DeepEmbeddedRoot { Level1 = new DeepLevel1 { Level2 = null } }),
				accessor(new DeepEmbeddedRoot { Level1 = new DeepLevel1 { Level2 = new DeepLevel2 { StringLeaf = null } } }),
				accessor(new DeepEmbeddedRoot { Level1 = new DeepLevel1 { Level2 = new DeepLevel2 { StringLeaf = new DeepStringLeaf { Text = null } } } }),
			};

			Assert.Equal(["", "", "", ""], results);
		}

		[Fact]
		public void BuildEmbeddedPropertyAccess_MultipleLayers_ValueTypePath_ReturnsDefaultWhenAnyNavigationIsNull()
		{
			var (property, accessor) = BuildEmbeddedAccessor<DeepEmbeddedRoot, int>(
				nameof(DeepEmbeddedRoot.Level1),
				nameof(DeepLevel1.Level2),
				nameof(DeepLevel2.NumberLeaf),
				nameof(DeepNumberLeaf.Number));

			var results = new[]
			{
				accessor(new DeepEmbeddedRoot
				{
					Level1 = new DeepLevel1
					{
						Level2 = new DeepLevel2
						{
							NumberLeaf = new DeepNumberLeaf
							{
								Number = 42
							}
						}
					}
				}),
				accessor(new DeepEmbeddedRoot { Level1 = null }),
				accessor(new DeepEmbeddedRoot { Level1 = new DeepLevel1 { Level2 = null } }),
				accessor(new DeepEmbeddedRoot { Level1 = new DeepLevel1 { Level2 = new DeepLevel2 { NumberLeaf = null } } }),
			};

			Assert.Equal(nameof(DeepNumberLeaf.Number), property.Name);
			Assert.Equal([42, 0, 0, 0], results);
		}

		private static (System.Reflection.PropertyInfo Property, Func<TModel, TValue> Accessor) BuildEmbeddedAccessor<TModel, TValue>(
			string rootPropertyName,
			params string[] parts)
		{
			var modelExpression = System.Linq.Expressions.Expression.Parameter(typeof(TModel), "x");
			var rootProperty = typeof(TModel).GetProperty(
				rootPropertyName,
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)!;

			var method = typeof(UserQueryExtensions).GetMethod(
				"BuildEmbeddedPropertyAccess",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

			var result = ((System.Reflection.PropertyInfo Property, System.Linq.Expressions.Expression Expression))method.Invoke(
				null,
				new object?[] { modelExpression, rootProperty, parts })!;

			var body = result.Expression.Type == typeof(TValue)
				? result.Expression
				: System.Linq.Expressions.Expression.Convert(result.Expression, typeof(TValue));

			var accessor = System.Linq.Expressions.Expression.Lambda<Func<TModel, TValue>>(body, modelExpression).Compile();
			return (result.Property, accessor);
		}

		private sealed class DeepEmbeddedRoot
		{
			public DeepLevel1? Level1 { get; init; }
		}

		private sealed class DeepLevel1
		{
			public DeepLevel2? Level2 { get; init; }
		}

		private sealed class DeepLevel2
		{
			public DeepStringLeaf? StringLeaf { get; init; }

			public DeepNumberLeaf? NumberLeaf { get; init; }
		}

		private sealed class DeepStringLeaf
		{
			public string? Text { get; init; }
		}

		private sealed class DeepNumberLeaf
		{
			public int Number { get; init; }
		}
	}
}
