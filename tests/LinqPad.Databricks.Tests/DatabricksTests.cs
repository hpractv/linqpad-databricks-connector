using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LinqPad.Databricks.Driver.Ado;
using LinqPad.Databricks.Driver.Http;

namespace LinqPad.Databricks.Tests
{
    public class DatabricksConnectionTests
    {
        private const string WorkspaceUrl = "https://adb-123.azuredatabricks.net";
        private const string Pat = "test-pat";
        private const string WarehouseId = "wh-abc";

        [Fact]
        public void Open_SetsStateToOpen()
        {
            var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            Assert.Equal(ConnectionState.Closed, conn.State);
            conn.Open();
            Assert.Equal(ConnectionState.Open, conn.State);
        }

        [Fact]
        public void Open_CreatesExecutionClient()
        {
            var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            Assert.Null(conn.ExecutionClient);
            conn.Open();
            Assert.NotNull(conn.ExecutionClient);
        }

        [Fact]
        public void Open_Idempotent_DoesNotThrow()
        {
            var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            conn.Open();
            conn.Open(); // second call should be a no-op
            Assert.Equal(ConnectionState.Open, conn.State);
        }

        [Fact]
        public void Close_SetsStateToClosed()
        {
            var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            conn.Open();
            conn.Close();
            Assert.Equal(ConnectionState.Closed, conn.State);
        }

        [Fact]
        public void Close_NullsExecutionClient()
        {
            var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            conn.Open();
            conn.Close();
            Assert.Null(conn.ExecutionClient);
        }

        [Fact]
        public void ConnectionString_ContainsWorkspaceUrlAndWarehouseId()
        {
            var conn = new DatabricksConnection(WorkspaceUrl, Pat, WarehouseId);
            Assert.Contains(WorkspaceUrl, conn.ConnectionString);
            Assert.Contains(WarehouseId, conn.ConnectionString);
        }

        [Fact]
        public void Constructor_NullWorkspaceUrl_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DatabricksConnection(null!, Pat, WarehouseId));
        }

        [Fact]
        public void Constructor_NullPat_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DatabricksConnection(WorkspaceUrl, null!, WarehouseId));
        }

        [Fact]
        public void Constructor_NullWarehouseId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new DatabricksConnection(WorkspaceUrl, Pat, null!));
        }
    }
}

