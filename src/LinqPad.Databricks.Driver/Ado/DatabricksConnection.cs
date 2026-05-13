using System;
using System.Data;
using System.Data.Common;
using LinqPad.Databricks.Driver.Http;
using LinqPad.Databricks.Driver.Sql;

namespace LinqPad.Databricks.Driver.Ado
{
    public sealed class DatabricksConnection : DbConnection
    {
        private readonly string _pat;
        private ConnectionState _state = ConnectionState.Closed;

        internal string WorkspaceUrl { get; }
        internal string WarehouseId { get; }
        internal StatementExecutionClient? ExecutionClient { get; private set; }

        public DatabricksConnection(string workspaceUrl, string pat, string warehouseId)
        {
            WorkspaceUrl = workspaceUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(workspaceUrl));
            _pat = pat ?? throw new ArgumentNullException(nameof(pat));
            WarehouseId = warehouseId ?? throw new ArgumentNullException(nameof(warehouseId));
        }

        public override string ConnectionString
        {
            get => $"WorkspaceUrl={WorkspaceUrl};WarehouseId={WarehouseId}";
#pragma warning disable CS8765
            set { /* no-op for REST-based connection */ }
#pragma warning restore CS8765
        }

        public override string Database => string.Empty;
        public override string DataSource => WorkspaceUrl;
        public override string ServerVersion => "0.0";
        public override ConnectionState State => _state;

        public override void Open()
        {
            if (_state == ConnectionState.Open) return;
            // REST is stateless; just build the execution client here
            var http = new DatabricksHttpClient(WorkspaceUrl, _pat);
            ExecutionClient = new StatementExecutionClient(http);
            _state = ConnectionState.Open;
        }

        public override void Close()
        {
            _state = ConnectionState.Closed;
            ExecutionClient = null;
        }

        protected override DbCommand CreateDbCommand() => new DatabricksCommand(this);

        public override void ChangeDatabase(string databaseName) =>
            throw new NotSupportedException("ChangeDatabase is not supported.");

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException("Transactions are not supported.");
    }
}
