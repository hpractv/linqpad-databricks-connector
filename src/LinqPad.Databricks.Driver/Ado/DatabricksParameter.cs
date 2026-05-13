using System;
using System.Data;
using System.Data.Common;

namespace LinqPad.Databricks.Driver.Ado
{
    public sealed class DatabricksParameter : DbParameter
    {
        public override DbType DbType { get; set; } = DbType.String;
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
#pragma warning disable CS8765
        public override string ParameterName { get; set; } = string.Empty;
        public override string SourceColumn { get; set; } = string.Empty;
#pragma warning restore CS8765
        public override int Size { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() => DbType = DbType.String;
    }

    internal sealed class DatabricksParameterCollection : DbParameterCollection
    {
        private readonly System.Collections.ArrayList _list = new();

        public override int Count => _list.Count;
        public override object SyncRoot => _list.SyncRoot;

        public override int Add(object value) => _list.Add(value);
        public override void AddRange(Array values) => _list.AddRange(values);
        public override void Clear() => _list.Clear();
        public override bool Contains(object value) => _list.Contains(value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => _list.CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _list.GetEnumerator();
        public override int IndexOf(object value) => _list.IndexOf(value);

        public override int IndexOf(string parameterName)
        {
            for (int i = 0; i < _list.Count; i++)
                if (_list[i] is DatabricksParameter p && p.ParameterName == parameterName) return i;
            return -1;
        }

        public override void Insert(int index, object value) => _list.Insert(index, value);
        public override void Remove(object value) => _list.Remove(value);
        public override void RemoveAt(int index) => _list.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => (DbParameter)_list[index]!;
        protected override DbParameter GetParameter(string parameterName) => GetParameter(IndexOf(parameterName));
        protected override void SetParameter(int index, DbParameter value) => _list[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => SetParameter(IndexOf(parameterName), value);
    }
}
