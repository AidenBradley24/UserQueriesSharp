using UserQueries;

namespace Tests
{
	[PrimaryUserQueryable(nameof(Name))]
	internal class TestModel
	{
		public int Id { get; set; }

		[UserQueryable("name")]
		public string Name { get; set; } = "";

		[UserQueryable("intvalue")]
		public int IntValue { get; set; } = 0;

		[UserQueryable("floatvalue")]
		public float FloatValue { get; set; } = 0.0f;

		[UserQueryable("doublevalue")]
		public double DoubleValue { get; set; } = 0.0;

		[UserQueryable("timespanvalue")]
		public TimeSpan TimeSpanValue { get; set; } = TimeSpan.FromSeconds(1);
	}
}
