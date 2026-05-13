using System;
using System.Collections.Generic;
using System.Reflection;
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
                return string.IsNullOrEmpty(url) ? "Databricks workspace (not set)" : url;
            }
            catch
            {
                return "Databricks workspace";
            }
        }

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(
            IConnectionInfo cxInfo,
            AssemblyName assemblyToBuild,
            ref string nameSpace,
            ref string typeName)
        {
            nameSpace = "LinqPad.Databricks.Runtime";
            typeName = "DatabricksDataContext";
            return new List<ExplorerItem>();
        }
    }
}
