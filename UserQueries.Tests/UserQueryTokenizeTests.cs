using static UserQueries.Tokenizer;

namespace UserQueries.Tests
{
	public class UserQueryTokenizeTests
	{
		[Theory]
		[InlineData(null, new string[0], new bool[0])]
		[InlineData("", new string[0], new bool[0])]
		[InlineData("abc", new[] { "abc" }, new[] { false })]
		[InlineData("123", new[] { "123" }, new[] { true })]
		[InlineData("abc 123", new[] { "abc", "123" }, new[] { false, true })]
		[InlineData("'abc def'", new[] { "abc def" }, new[] { true })]
		[InlineData("\"abc def\"", new[] { "abc def" }, new[] { true })]
		[InlineData("abc,def", new[] { "abc", ",", "def" }, new[] { false, false, false })]
		[InlineData("abc & def", new[] { "abc", "&", "def" }, new[] { false, false, false })]
		[InlineData("a\\ b", new[] { "a", "\\", "b" }, new[] { false, false, false })]
		[InlineData("'a\\'b'", new[] { "a'b" }, new[] { true })]
		[InlineData("\"a\\\"b\"", new[] { "a\"b" }, new[] { true })]
		[InlineData("  abc  ", new[] { "abc" }, new[] { false })]
		[InlineData("1, 'two', \"three\"", new[] { "1", ",", "two", ",", "three" }, new[] { true, false, true, false, true })]
		[InlineData("12>=2 17=2, beans='text'", new[] { "12", ">=", "2", "17", "=", "2", ",", "beans", "=", "text" }, new[] { true, false, true, true, false, true, false, false, false, true })]
		public void Tokenize_BasicCases(string input, string[] expectedValues, bool[] expectedLiterals)
		{
			var tokens = Tokenize(input).ToArray();
			Assert.Equal(expectedValues, tokens.Select(t => t.Value));
			Assert.Equal(expectedLiterals, tokens.Select(t => t.IsLiteral));
		}

		[Fact]
		public void Tokenize_HandlesMultipleDelimiters()
		{
			var input = "a, b & c";
			var tokens = Tokenize(input).ToArray();
			Assert.Equal(["a", ",", "b", "&", "c"], tokens.Select(t => t.Value));
		}

		[Fact]
		public void Tokenize_HandlesNumbersAsLiterals()
		{
			var input = "42 test";
			var tokens = Tokenize(input).ToArray();
			Assert.Equal(["42", "test"], tokens.Select(t => t.Value));
			Assert.Equal([true, false], tokens.Select(t => t.IsLiteral));
		}

		[Theory]
		[InlineData("1.0", new[] { "1.0" }, new[] { true })]
		[InlineData("  1.0  ", new[] { "1.0" }, new[] { true })]
		[InlineData("4.2, abc", new[] { "4.2", ",", "abc" }, new[] { true, false, false })]
		[InlineData("beans <=4.6", new[] { "beans", "<=", "4.6" }, new[] { false, false, true })]
		[InlineData("beans > 5.1!", new[] { "beans", ">", "5.1", "!" }, new[] { false, false, true, false })]
		[InlineData("beans > 5.1'potatoes'", new[] { "beans", ">", "5.1", "potatoes" }, new[] { false, false, true, true })]
		public void Tokenize_HandlesDecimalNumbers(string input, string[] expectedValues, bool[] expectedLiterals)
		{
			var tokens = Tokenize(input).ToArray();
			Assert.Equal(expectedValues, tokens.Select(t => t.Value));
			Assert.Equal(expectedLiterals, tokens.Select(t => t.IsLiteral));
		}

		[Theory]
		[InlineData("a < -1", new[] { "a", "<", "-1" }, new[] { false, false, true })]
		[InlineData("a<-1", new[] { "a", "<", "-1" }, new[] { false, false, true })]
		[InlineData("a1 : 1 - 5", new[] { "a1", ":", "1", "-", "5" }, new[] { false, false, true, false, true })]
		[InlineData("a1: 1-5", new[] { "a1", ":", "1", "-", "5" }, new[] { false, false, true, false, true })]
		[InlineData("a1 : 1 - -5", new[] { "a1", ":", "1", "-", "-5" }, new[] { false, false, true, false, true })]
		[InlineData("a1: 1--5", new[] { "a1", ":", "1", "-", "-5" }, new[] { false, false, true, false, true })]
		[InlineData("a1: -1-5", new[] { "a1", ":", "-1", "-", "5" }, new[] { false, false, true, false, true })]
		[InlineData("a>=-1", new[] { "a", ">=", "-1" }, new[] { false, false, true })]
		[InlineData("a---1", new[] { "a", "--", "-1" }, new[] { false, false, true })]
		[InlineData("a-1", new[] { "a", "-", "1" }, new[] { false, false, true })]
		public void Tokenize_HandlesNegativeNumbers(string input, string[] expectedValues, bool[] expectedLiterals)
		{
			var tokens = Tokenize(input).ToArray();
			Assert.Equal(expectedValues, tokens.Select(t => t.Value));
			Assert.Equal(expectedLiterals, tokens.Select(t => t.IsLiteral));
		}
		[Theory]
		[InlineData("var_name=5", new[] { "var_name", "=", "5" }, new[] { false, false, true })]
		[InlineData("var_name_2 = 10", new[] { "var_name_2", "=", "10" }, new[] { false, false, true })]
		[InlineData("_privateVar=42", new[] { "_privateVar", "=", "42" }, new[] { false, false, true })]
		[InlineData("a_b_c, x_y_z", new[] { "a_b_c", ",", "x_y_z" }, new[] { false, false, false })]
		[InlineData("beans_var <=4.6", new[] { "beans_var", "<=", "4.6" }, new[] { false, false, true })]
		public void Tokenize_HandlesUnderscoresInNames(string input, string[] expectedValues, bool[] expectedLiterals)
		{
			var tokens = Tokenize(input).ToArray();
			Assert.Equal(expectedValues, tokens.Select(t => t.Value));
			Assert.Equal(expectedLiterals, tokens.Select(t => t.IsLiteral));
		}
	}
}