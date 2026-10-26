namespace DataverseGen.Core.Reporting;

/// <summary>
///  Masks secrets (see <see cref="SecretMasker" />) in everything that passes through, so sinks behind it never
///  see a raw connection string. Thread-safe when the wrapped reporter is.
/// </summary>
public sealed class MaskingReporter : IGenerationReporter
{
	private readonly IGenerationReporter _inner;

	public MaskingReporter(IGenerationReporter inner)
	{
		_inner = inner ?? NullReporter.Instance;
	}

	public void Log(LogLevel level, string message, Exception exception = null)
	{
		_inner.Log(level, SecretMasker.MaskSecrets(message), exception is null ? null : new MaskedException(exception));
	}

	public void StageStarted(Stage stage, int total)
	{
		_inner.StageStarted(stage, total);
	}

	public void ItemStarted(ItemKind kind, ItemPhase phase, string name)
	{
		_inner.ItemStarted(kind, phase, SecretMasker.MaskSecrets(name));
	}

	public void ItemCompleted(ItemResult result)
	{
		_inner.ItemCompleted(new ItemResult
		{
			Kind = result.Kind,
			Phase = result.Phase,
			Name = SecretMasker.MaskSecrets(result.Name),
			Status = result.Status,
			Detail = SecretMasker.MaskSecrets(result.Detail),
			Elapsed = result.Elapsed,
			Files = result.Files,
			Bytes = result.Bytes
		});
	}

	public void StageCompleted(Stage stage, TimeSpan elapsed)
	{
		_inner.StageCompleted(stage, elapsed);
	}

	public void RequestCompleted(Stage stage, TimeSpan elapsed)
	{
		_inner.RequestCompleted(stage, elapsed);
	}

	private sealed class MaskedException : Exception
	{
		private readonly string _text;

		public MaskedException(Exception original) : base(SecretMasker.MaskSecrets(original.Message))
		{
			_text = SecretMasker.MaskSecrets(original.ToString());
		}

		public override string ToString()
		{
			return _text;
		}
	}
}
