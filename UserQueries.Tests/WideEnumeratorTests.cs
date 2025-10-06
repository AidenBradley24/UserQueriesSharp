using UserQueries;

namespace Tests
{
	public class WideEnumeratorTests
	{
		private readonly int[] testValues = [1, 2, 3, 4, 5, 6, 7];

		[Fact]
		public void Run()
		{
			var enumerator = testValues.GetWideEnumerator(2, 3);
			for (int i = 0; i < 5; i++)
			{
				enumerator.MoveNext();
			}
		}


		[Fact]
		public void Test()
		{
			var enumerator = testValues.GetWideEnumerator(2, 3);

			Assert.Equal([1, 2, 3], enumerator.Foresight);
			Assert.Empty(enumerator.History);
			Assert.Throws<InvalidOperationException>(() => enumerator.Current);

			Assert.True(enumerator.MoveNext());
			Assert.Equal(1, enumerator.Current);
			Assert.Equal([2, 3, 4], enumerator.Foresight);
			Assert.Empty(enumerator.History);

			Assert.True(enumerator.MoveNext());
			Assert.Equal(2, enumerator.Current);
			Assert.Equal([3, 4, 5], enumerator.Foresight);
			Assert.Equal([1], enumerator.History);

			Assert.True(enumerator.MoveNext());
			Assert.Equal(3, enumerator.Current);
			Assert.Equal([4, 5, 6], enumerator.Foresight);
			Assert.Equal([2, 1], enumerator.History);

			Assert.True(enumerator.MoveNext());
			Assert.Equal(4, enumerator.Current);
			Assert.Equal([5, 6, 7], enumerator.Foresight);
			Assert.Equal([3, 2], enumerator.History);

			Assert.True(enumerator.MoveNext());
			Assert.Equal(5, enumerator.Current);
			Assert.Equal([6, 7], enumerator.Foresight);
			Assert.Equal([4, 3], enumerator.History);

			Assert.True(enumerator.MoveNext());
			Assert.Equal(6, enumerator.Current);
			Assert.Equal([7], enumerator.Foresight);
			Assert.Equal([5, 4], enumerator.History);

			Assert.True(enumerator.MoveNext());
			Assert.Equal(7, enumerator.Current);
			Assert.Empty(enumerator.Foresight);
			Assert.Equal([6, 5], enumerator.History);

			Assert.False(enumerator.MoveNext());
		}
	}
}
