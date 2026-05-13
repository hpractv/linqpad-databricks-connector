using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;

namespace LinqPad.Databricks.Driver
{
    public class DatabricksDriver : DynamicDataContextDriver
    {
        public override string Name => "Databricks LINQPad Driver";
        public override string Author => "Your Name";

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            try
            {
                var data = cxInfo.DriverData;
                if (data == null) return "Databricks (no workspace URL)";
                var urlEl = data.Element("WorkspaceUrl");
                return urlEl != null && !string.IsNullOrWhiteSpace(urlEl.Value) ? urlEl.Value : "Databricks (no workspace URL)";
            }
            catch
            {
                return "Databricks";
            }
        }

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(
            IConnectionInfo cxInfo,
            AssemblyName assemblyToBuild,
            ref string nameSpace,
            ref string typeName)
        {
            // Minimal stub: return empty schema and rely on template helpers for real implementation
            nameSpace = "LinqPad.Databricks.Generated";
            typeName = "DatabricksDataContext";
            return new List<ExplorerItem>();
        }
    }
}
