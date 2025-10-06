using CitizenFX.Core;
using System;
using System.Collections.Generic;

public class DatabaseCore : BaseScript
{
	public DatabaseCore()
	{
		Debug.WriteLine("[Database] loaded.");
		RegisterExports();
	}

	// SELECT
	public void Query(string query, IDictionary<string, object> parameters, CallbackDelegate cb)
	{
		Exports["oxmysql"].execute(query, parameters, new Action<dynamic>(rows => cb?.Invoke(rows)));
	}

	// SCALAR
	public void Scalar(string query, IDictionary<string, object> parameters, CallbackDelegate cb)
	{
		Exports["oxmysql"].scalar(query, parameters, new Action<dynamic>(val => cb?.Invoke(val)));
	}

	// INSERT (returns inserted ID)
	public void Insert(string query, IDictionary<string, object> parameters, CallbackDelegate cb)
	{
		Exports["oxmysql"].insert(query, parameters, new Action<dynamic>(id => cb?.Invoke(id)));
	}

	private void RegisterExports()
	{
		Exports.Add("Query", new Action<string, IDictionary<string, object>, CallbackDelegate>(Query));
		Exports.Add("Scalar", new Action<string, IDictionary<string, object>, CallbackDelegate>(Scalar));
		Exports.Add("Insert", new Action<string, IDictionary<string, object>, CallbackDelegate>(Insert));
	}
}
