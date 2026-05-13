using System;
using System.Reflection;
using System.Xml.Linq;
using LINQPad.Extensibility.DataContext;
using System.Collections.Generic;

namespace LinqPad.Databricks.Driver
{
    public class DatabricksDriver : DynamicDataContextDriver
    {
        public override string Name => "Databricks LINQPad Driver";
        public override string Author => "hpractv";
        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            var url = "Databricks workspace";
            try
            {
                var el = cxInfo?.DriverData?.Element("WorkspaceUrl");
                if (el != null) url = el.Value;
            }
            catch { }
            return url;
        }

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
        {
            nameSpace ??= "LinqPad.Databricks";
            typeName ??= "DatabricksContext";
            return new List<ExplorerItem>();
        }
    }
}
