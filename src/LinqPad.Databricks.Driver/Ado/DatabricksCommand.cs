using System;
using System.Data;
using System.Data.Common;

namespace LinqPad.Databricks.Driver.Ado
{
    public sealed class DatabricksCommand : DbCommand
    {
        private readonly DatabricksConnection _connection;

        public DatabricksCommand(DatabricksConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

#pragma warning disable CS8765
        public override string CommandText { get; set; } = string.Empty;
#pragma warning restore CS8765
        public override int CommandTimeout { get; set; } = 120;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection
        {
#pragma warning disable CS8765
            get => _connection;
            set { /* no-op — connection is fixed at construction */ }
#pragma warning restore CS8765
        }

        protected override DbParameterCollection DbParameterCollection { get; } = new DatabricksParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { /* REST calls cannot be cancelled mid-flight via this interface */ }
        public override void Prepare() { /* no-op */ }

        protected override DbParameter CreateDbParameter() => new DatabricksParameter();

        public override int ExecuteNonQuery()
        {
            var dt = ExecuteInternal();
            return dt.Rows.Count;
        }

        public override object? ExecuteScalar()
        {
            var dt = ExecuteInternal();
            if (dt.Rows.Count == 0 || dt.Columns.Count == 0)
                return null;
            var val = dt.Rows[0][0];
            return val == DBNull.Value ? null : val;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var dt = ExecuteInternal();
            return dt.CreateDataReader();
        }

        private DataTable ExecuteInternal()
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            var exec = _connection.ExecutionClient
                ?? throw new InvalidOperationException("Connection is not open.");

            return exec.ExecuteAsync(_connection.WarehouseId, CommandText)
                       .GetAwaiter().GetResult();
        }
    }
}
