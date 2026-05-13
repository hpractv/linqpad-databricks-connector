using System.Data;
using System.Data.Common;
using LinqPad.Databricks.Driver.Client;

namespace LinqPad.Databricks.Driver.Ado;

// ─── Connection ───────────────────────────────────────────────────────────────

public class DatabricksConnection : DbConnection
{
    private string _workspaceUrl = string.Empty;
    private string _pat = string.Empty;
    private string _warehouseId = string.Empty;
    private ConnectionState _state = ConnectionState.Closed;

    public DatabricksConnection() { }

    public DatabricksConnection(string workspaceUrl, string pat, string warehouseId)
    {
        _workspaceUrl = workspaceUrl;
        _pat = pat;
        _warehouseId = warehouseId;
    }

    // Connection string format: WorkspaceUrl=<url>;WarehouseId=<id>;Pat=<token>
    public override string ConnectionString
    {
        get => $"WorkspaceUrl={_workspaceUrl};WarehouseId={_warehouseId};Pat=***";
        set
        {
            if (value == null) return;
            var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                int eq = part.IndexOf('=');
                if (eq < 0) continue;
                string key = part[..eq].Trim();
                string val = part[(eq + 1)..].Trim();
                switch (key.ToLowerInvariant())
                {
                    case "workspaceurl": _workspaceUrl = val; break;
                    case "warehouseid": _warehouseId = val; break;
                    case "pat": _pat = val; break;
                }
            }
        }
    }

    public string WorkspaceUrl => _workspaceUrl;
    public string PersonalAccessToken => _pat;
    public string WarehouseId => _warehouseId;

    public override string Database => string.Empty;
    public override string DataSource => _workspaceUrl;
    public override string ServerVersion => "Databricks";
    public override ConnectionState State => _state;

    public override void Open() => _state = ConnectionState.Open;
    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException("Databricks does not support transactions.");

    protected override DbCommand CreateDbCommand() =>
        new DatabricksCommand { Connection = this };

    internal DatabricksHttpClient CreateHttpClient() =>
        new DatabricksHttpClient(_workspaceUrl, _pat);

    internal StatementExecutionClient CreateStatementClient() =>
        new StatementExecutionClient(CreateHttpClient());

    internal UnityCatalogClient CreateUnityCatalogClient() =>
        new UnityCatalogClient(CreateHttpClient());
}

// ─── Command ──────────────────────────────────────────────────────────────────

public class DatabricksCommand : DbCommand
{
    private string _commandText = string.Empty;
    private DatabricksConnection? _connection;

    public override string CommandText { get => _commandText; set => _commandText = value ?? string.Empty; }
    public override int CommandTimeout { get; set; } = 120;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => _connection;
        set => _connection = (DatabricksConnection?)value;
    }

    protected override DbParameterCollection DbParameterCollection => _params ??= new DatabricksParameterCollection();
    private DatabricksParameterCollection? _params;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }

    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new DatabricksParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        if (_connection == null) throw new InvalidOperationException("Connection is not set.");
        StatementExecutionClient client = _connection.CreateStatementClient();
        StatementResult result = client.ExecuteAsync(_connection.WarehouseId, _commandText).GetAwaiter().GetResult();
        return new DatabricksDataReader(result);
    }

    public override int ExecuteNonQuery()
    {
        if (_connection == null) throw new InvalidOperationException("Connection is not set.");
        StatementExecutionClient client = _connection.CreateStatementClient();
        client.ExecuteAsync(_connection.WarehouseId, _commandText).GetAwaiter().GetResult();
        return -1;
    }

    public override object? ExecuteScalar()
    {
        using DbDataReader reader = ExecuteDbDataReader(CommandBehavior.Default);
        if (reader.Read() && reader.FieldCount > 0)
            return reader.GetValue(0);
        return null;
    }
}

// ─── DataReader ───────────────────────────────────────────────────────────────

public class DatabricksDataReader : DbDataReader
{
    private readonly StatementResult _result;
    private int _rowIndex = -1;

    public DatabricksDataReader(StatementResult result) => _result = result;

    public override int FieldCount => _result.Columns.Count;
    public override bool HasRows => _result.Rows.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => -1;
    public override int Depth => 0;

    public override bool Read()
    {
        _rowIndex++;
        return _rowIndex < _result.Rows.Count;
    }

    public override bool NextResult() => false;

    private IReadOnlyList<string?> CurrentRow
    {
        get
        {
            if (_rowIndex < 0 || _rowIndex >= _result.Rows.Count)
                throw new InvalidOperationException("No current row.");
            return _result.Rows[_rowIndex];
        }
    }

    public override object GetValue(int i)
    {
        string? val = CurrentRow.Count > i ? CurrentRow[i] : null;
        return val ?? (object)DBNull.Value;
    }

    public override string GetName(int i) => _result.Columns[i].Name;

    public override int GetOrdinal(string name)
    {
        for (int i = 0; i < _result.Columns.Count; i++)
            if (string.Equals(_result.Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        throw new IndexOutOfRangeException($"Column '{name}' not found.");
    }

    public override string GetDataTypeName(int i) => _result.Columns[i].TypeName;

    public override Type GetFieldType(int i) => typeof(string);

    public override int GetValues(object[] values)
    {
        int count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++) values[i] = GetValue(i);
        return count;
    }

    public override bool IsDBNull(int i)
    {
        string? val = CurrentRow.Count > i ? CurrentRow[i] : null;
        return val is null;
    }

    public override bool GetBoolean(int i) => bool.Parse(CurrentRow[i] ?? "false");
    public override byte GetByte(int i) => byte.Parse(CurrentRow[i] ?? "0");
    public override long GetBytes(int i, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override char GetChar(int i) => (CurrentRow[i] ?? string.Empty)[0];
    public override long GetChars(int i, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override Guid GetGuid(int i) => Guid.Parse(CurrentRow[i] ?? Guid.Empty.ToString());
    public override short GetInt16(int i) => short.Parse(CurrentRow[i] ?? "0");
    public override int GetInt32(int i) => int.Parse(CurrentRow[i] ?? "0");
    public override long GetInt64(int i) => long.Parse(CurrentRow[i] ?? "0");
    public override float GetFloat(int i) => float.Parse(CurrentRow[i] ?? "0");
    public override double GetDouble(int i) => double.Parse(CurrentRow[i] ?? "0");
    public override string GetString(int i) => CurrentRow[i] ?? string.Empty;
    public override decimal GetDecimal(int i) => decimal.Parse(CurrentRow[i] ?? "0");
    public override DateTime GetDateTime(int i) => DateTime.Parse(CurrentRow[i] ?? DateTime.MinValue.ToString());

    public override System.Collections.IEnumerator GetEnumerator() =>
        throw new NotSupportedException();

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
}

// ─── Parameter (stub) ─────────────────────────────────────────────────────────

public class DatabricksParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; }
    public override bool IsNullable { get; set; }
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() { }
}

// ─── ParameterCollection (stub) ───────────────────────────────────────────────

public class DatabricksParameterCollection : DbParameterCollection
{
    private readonly List<DatabricksParameter> _inner = new();

    public override int Count => _inner.Count;
    public override object SyncRoot => _inner;

    public override int Add(object value) { _inner.Add((DatabricksParameter)value); return _inner.Count - 1; }
    public override void AddRange(Array values) { foreach (var v in values) Add(v); }
    public override void Clear() => _inner.Clear();
    public override bool Contains(object value) => _inner.Contains((DatabricksParameter)value);
    public override bool Contains(string value) => _inner.Any(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) => ((System.Collections.Generic.ICollection<DatabricksParameter>)_inner).CopyTo((DatabricksParameter[])array, index);
    public override System.Collections.IEnumerator GetEnumerator() => _inner.GetEnumerator();
    public override int IndexOf(object value) => _inner.IndexOf((DatabricksParameter)value);
    public override int IndexOf(string parameterName) => _inner.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _inner.Insert(index, (DatabricksParameter)value);
    public override void Remove(object value) => _inner.Remove((DatabricksParameter)value);
    public override void RemoveAt(int index) => _inner.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _inner.RemoveAll(p => p.ParameterName == parameterName);
    protected override DbParameter GetParameter(int index) => _inner[index];
    protected override DbParameter GetParameter(string parameterName) => _inner.First(p => p.ParameterName == parameterName);
    protected override void SetParameter(int index, DbParameter value) => _inner[index] = (DatabricksParameter)value;
    protected override void SetParameter(string parameterName, DbParameter value) => _inner[IndexOf(parameterName)] = (DatabricksParameter)value;
}

// ─── ProviderFactory ──────────────────────────────────────────────────────────

public class DatabricksProviderFactory : DbProviderFactory
{
    public static readonly DatabricksProviderFactory Instance = new();
    private DatabricksProviderFactory() { }

    public override DbConnection CreateConnection() => new DatabricksConnection();
    public override DbCommand CreateCommand() => new DatabricksCommand();
    public override DbParameter CreateParameter() => new DatabricksParameter();
    public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new DbConnectionStringBuilder();
}
