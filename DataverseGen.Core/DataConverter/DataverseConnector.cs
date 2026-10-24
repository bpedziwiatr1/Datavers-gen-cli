using System.Diagnostics;
using DataverseGen.Core.ConnectionString;
using DataverseGen.Core.Reporting;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace DataverseGen.Core.DataConverter;

public class DataverseConnector : IDisposable
{
	private readonly string _connectionString;
	private readonly bool _isConnectionStringValidatorEnabled;
	private readonly IGenerationReporter _reporter;
	private bool _disposed;

	public DataverseConnector(
		string connectionString,
		bool isConnectionStringValidatorEnabled,
		IGenerationReporter reporter = null)
	{
		_connectionString = connectionString;
		_isConnectionStringValidatorEnabled = isConnectionStringValidatorEnabled;
		_reporter = reporter ?? NullReporter.Instance;
		WriteConnectorInfo();
		ValidateConnectionStringIfEnabled();
	}

	public ServiceClient OrganizationService { get; private set; }

	public void Connect()
	{
		Stopwatch stopper = Stopwatch.StartNew();
		_reporter.StageStarted(Stage.Connect, 1);
		_reporter.Log(LogLevel.Info, "Waiting for connection...");
		OrganizationService = new ServiceClient(_connectionString);
		WaitForConnection();

		CheckIfThereWasErrorOnConnecting();
		_reporter.StageCompleted(Stage.Connect, stopper.Elapsed);
	}

	private void CheckIfThereWasErrorOnConnecting()
	{
		if (string.IsNullOrWhiteSpace(OrganizationService.LastError))
		{
			return;
		}

		string exceptionMessage = SecretMasker.MaskSecrets(
			$"Connection did not connect with {_connectionString}. LastCrmError: {OrganizationService.LastError} | {OrganizationService.LastException}");

		throw new Exception(exceptionMessage);
	}

	private void ValidateConnectionStringIfEnabled()
	{
		if (_isConnectionStringValidatorEnabled)
		{
			new ConnectionStringValidator(_connectionString).Validate();
		}
	}

	private void WaitForConnection()
	{
		const int waitForConnection = 1000;

		while (!OrganizationService.IsReady)
		{
			_reporter.Log(LogLevel.Debug, $@"Waiting for connection... {waitForConnection}ms");

			if (OrganizationService.LastException != null)
			{
				throw OrganizationService.LastException;
			}

			Thread.Sleep(waitForConnection);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		OrganizationService?.Dispose();
		_disposed = true;
	}

	private void WriteConnectorInfo()
	{
		_reporter.Log(LogLevel.Debug, $"Validate ConnectionsString {_isConnectionStringValidatorEnabled}");
	}
}
