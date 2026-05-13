using LinqPad.Databricks.Driver.Ado;
using LinqPad.Databricks.Driver.Client;

namespace LinqPad.Databricks.Tests;

public class DatabricksDataReaderTests
{
    private static StatementResult MakeResult() => new StatementResult
    {
        Columns =
        [
            new ColumnSchema("id", "BIGINT"),
            new ColumnSchema("name", "STRING"),
            new ColumnSchema("score", "DOUBLE"),
        ],
        Rows =
        [
            new[] { "1", "Alice", "9.5" },
            new[] { "2", "Bob", null },
        ]
    };

    [Fact]
    public void FieldCount_ReturnsColumnCount()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.Equal(3, reader.FieldCount);
    }

    [Fact]
    public void GetName_ReturnsColumnName()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.Equal("id", reader.GetName(0));
        Assert.Equal("name", reader.GetName(1));
        Assert.Equal("score", reader.GetName(2));
    }

    [Fact]
    public void GetOrdinal_CaseInsensitive()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.Equal(0, reader.GetOrdinal("ID"));
        Assert.Equal(1, reader.GetOrdinal("Name"));
    }

    [Fact]
    public void GetOrdinal_UnknownColumn_Throws()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.Throws<IndexOutOfRangeException>(() => reader.GetOrdinal("nonexistent"));
    }

    [Fact]
    public void Read_IteratesRows()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.True(reader.Read());
        Assert.Equal("1", reader.GetValue(0));
        Assert.Equal("Alice", reader.GetValue(1));
        Assert.True(reader.Read());
        Assert.Equal("2", reader.GetValue(0));
        Assert.False(reader.Read());
    }

    [Fact]
    public void GetValue_NullCell_ReturnsDBNull()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        reader.Read();  // row 1
        reader.Read();  // row 2
        Assert.Equal(DBNull.Value, reader.GetValue(2)); // score is null
    }

    [Fact]
    public void IsDBNull_NullCell_ReturnsTrue()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        reader.Read(); reader.Read(); // row 2
        Assert.True(reader.IsDBNull(2));
        Assert.False(reader.IsDBNull(0));
    }

    [Fact]
    public void GetDataTypeName_ReturnsTypeName()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.Equal("BIGINT", reader.GetDataTypeName(0));
        Assert.Equal("DOUBLE", reader.GetDataTypeName(2));
    }

    [Fact]
    public void HasRows_NonEmptyResult_ReturnsTrue()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.True(reader.HasRows);
    }

    [Fact]
    public void HasRows_EmptyResult_ReturnsFalse()
    {
        var empty = new StatementResult
        {
            Columns = [new ColumnSchema("x", "INT")],
            Rows = []
        };
        using var reader = new DatabricksDataReader(empty);
        Assert.False(reader.HasRows);
    }

    [Fact]
    public void IndexerByName_ReturnsValue()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        reader.Read();
        Assert.Equal("Alice", reader["name"]);
    }

    [Fact]
    public void NextResult_ReturnsFalse()
    {
        using var reader = new DatabricksDataReader(MakeResult());
        Assert.False(reader.NextResult());
    }
}
