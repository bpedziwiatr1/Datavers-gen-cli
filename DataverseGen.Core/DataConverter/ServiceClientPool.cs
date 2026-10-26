using System.Collections.Concurrent;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DataverseGen.Core.DataConverter;

/// <summary>
///  Hands out <see cref="ServiceClient" /> instances to concurrent workers. With a degree of parallelism of 1 the
///  shared client is used as is; otherwise every concurrent worker gets its own <c>Clone()</c>, which is disposed
///  together with the pool. The shared client is never disposed here.
/// </summary>
internal sealed class ServiceClientPool : IDisposable
{
	private readonly ConcurrentBag<ServiceClient> _idle = new();
	private readonly ConcurrentBag<ServiceClient> _owned = new();
	private readonly ServiceClient _shared;
	private readonly bool _useClones;

	public ServiceClientPool(ServiceClient shared, int degreeOfParallelism)
	{
		_shared = shared ?? throw new ArgumentNullException(nameof(shared));
		_useClones = degreeOfParallelism > 1;
	}

	public ServiceClient Rent()
	{
		if (!_useClones)
		{
			return _shared;
		}

		if (_idle.TryTake(out ServiceClient client))
		{
			return client;
		}

		client = _shared.Clone() ?? throw new InvalidOperationException(
			$"Could not clone the Dataverse connection: {_shared.LastError}");
		_owned.Add(client);

		return client;
	}

	public void Return(ServiceClient client)
	{
		if (_useClones)
		{
			_idle.Add(client);
		}
	}

	public void Dispose()
	{
		foreach (ServiceClient client in _owned)
		{
			client.Dispose();
		}
	}
}
