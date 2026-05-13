using System;
using System.Reflection;
using System.Collections.Generic;
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
                var data = cxInfo?.DriverData;
                var wsUrl = data?.Element("WorkspaceUrl")?.Value;
                if (!string.IsNullOrEmpty(wsUrl))
                    return wsUrl;
            }
            catch { }
            return "Databricks Workspace";
        }

        // Minimal stub to satisfy abstract member requirements in DynamicDataContextDriver
        public override List<ExplorerItem> GetSchemaAndBuildAssembly(IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
        {
            nameSpace = "LinqPad.Databricks";
            typeName = "DatabricksContext";
            return new List<ExplorerItem>();
        }
    }
}
