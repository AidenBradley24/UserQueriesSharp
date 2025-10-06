#pragma warning disable CS1591
using System.Collections;

namespace UserQueries;

public static class WideEnumeratorExtensions
{
	public static IWideEnumerator<T> GetWideEnumerator<T>(this IEnumerable<T> enumerable, int historyDepth, int foresightDepth)
	{
		return new WideEnumerator<T>(enumerable.GetEnumerator(), historyDepth, foresightDepth);
	}
}

/// <summary>
/// Allows enumeration through any standard enumerator while keeping track of previous and upcoming values.
/// </summary>
public interface IWideEnumerator<T> : IEnumerator<T>
{
	public int HistoryDepth { get; }
	public int ForesightDepth { get; }

	/// <summary>
	/// The previous items in reverse occurance order.
	/// </summary>
	public IReadOnlyList<T> History { get; }

	/// <summary>
	/// The upcoming items in occurance order.
	/// </summary>
	public IReadOnlyList<T> Foresight { get; }

	/// <summary>
	/// Advances the enumerator by a specific number of elements
	/// </summary>
	/// <returns>true if the enumerator has advanced by <paramref name="length"/>. false if reached or surpassed the end of the collection.</returns>
	public bool MoveBy(int length);
}

/// <summary>
/// Allows enumeration through any standard enumerator while keeping track of previous and upcoming values.
/// </summary>
public class WideEnumerator<T> : IWideEnumerator<T>
{
	private readonly int TOTAL_SIZE;
	private readonly IEnumerator<T> baseEnumerator;
	private readonly T[] frame;
	private int frameIndex = 0;
	private int historyCount = 0;
	private int foresightCount = 0;
	private bool exhausted = false;
	bool started = false;

	private readonly ForesightCollection foresight;
	private readonly HistoryCollection history;
	
	public WideEnumerator(IEnumerator<T> baseEnumerator, int historyDepth, int foresightDepth)
	{
		this.baseEnumerator = baseEnumerator;
		TOTAL_SIZE = historyDepth + 1 + foresightDepth;
		frame = new T[TOTAL_SIZE];

		HistoryDepth = historyDepth;
		ForesightDepth = foresightDepth;

		foresight = new ForesightCollection(this);
		history = new HistoryCollection(this);

		// prewarm
		AdvanceForesight();
	}

	public T Current => started ? frame[CalcFrame(frameIndex - foresightCount)] : throw new InvalidOperationException();

	public int HistoryDepth { get; }

	public int ForesightDepth { get; }

	public IReadOnlyList<T> History => history;

	public IReadOnlyList<T> Foresight => foresight;

	object? IEnumerator.Current => Current;

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		baseEnumerator.Dispose();
	}

	public bool MoveNext()
	{
		if (exhausted && foresightCount == 0) return false;
		if (started && historyCount < HistoryDepth) historyCount++;
		started = true;
		foresightCount--;
		AdvanceForesight();
		return true;
	}

	private void AdvanceForesight()
	{
		while (!exhausted && foresightCount < ForesightDepth)
		{
			exhausted = !baseEnumerator.MoveNext();
			if (exhausted) break;
			frameIndex = CalcFrame(frameIndex + 1);
			frame[frameIndex] = baseEnumerator.Current;
			foresightCount++;
		}
	}

	public void Reset()
	{
		baseEnumerator.Reset();
		frameIndex = 0;
		foresightCount = 0;
		historyCount = 0;
		exhausted = false;
		started = false;

		// prewarm
		AdvanceForesight();
	}

	private int CalcFrame(int frameIndex)
	{
		int val = frameIndex % frame.Length;
		if (val < 0) val += frame.Length;
		return val;
	}

	public bool MoveBy(int length)
	{
		for (int i = 0; i < length; i++)
			if (!MoveNext()) return false;
		return true;
	}

	public class ForesightCollection(WideEnumerator<T> parent) : IReadOnlyList<T>
	{
		public T this[int index]
		{
			get
			{
				if (index < 0 || index > parent.foresightCount) throw new IndexOutOfRangeException();
				int point = parent.CalcFrame(parent.frameIndex - parent.foresightCount + index + 1);
				return parent.frame[point];
			}
		}

		public int Count => parent.foresightCount;

		public IEnumerator<T> GetEnumerator()
		{
			for (int i = 0; i < parent.foresightCount; i++)
			{
				int point = parent.CalcFrame(parent.frameIndex - parent.foresightCount + i + 1);
				yield return parent.frame[point];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	public class HistoryCollection(WideEnumerator<T> parent) : IReadOnlyList<T>
	{
		public T this[int index]
		{
			get
			{
				if (index < 0 || index > parent.historyCount) throw new IndexOutOfRangeException();
				int point = parent.CalcFrame(parent.frameIndex - parent.foresightCount - index - 1);
				return parent.frame[point];
			}
		}

		public int Count => parent.historyCount;

		public IEnumerator<T> GetEnumerator()
		{
			for (int i = 0; i < parent.historyCount; i++)
			{
				int point = parent.CalcFrame(parent.frameIndex - parent.foresightCount - i - 1);
				yield return parent.frame[point];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
