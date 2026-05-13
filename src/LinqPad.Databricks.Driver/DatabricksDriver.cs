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
        public override string Author => "hpractv";

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            try
            {
                var url = cxInfo?.DriverData?.Element("WorkspaceUrl")?.Value;
                return string.IsNullOrWhiteSpace(url) ? "Databricks workspace (unspecified)" : $"Databricks workspace: {url}";
            }
            catch
            {
                return "Databricks workspace (invalid DriverData)";
            }
        }

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(
            IConnectionInfo cxInfo,
            AssemblyName assemblyToBuild,
            ref string nameSpace,
            ref string typeName)
        {
            nameSpace = "LinqPad.Databricks.Generated";
            typeName = "DatabricksDataContext";
            return new List<ExplorerItem>();
        }
    }
}
