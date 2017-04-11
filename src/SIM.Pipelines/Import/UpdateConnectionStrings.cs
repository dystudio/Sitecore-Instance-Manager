﻿namespace SIM.Pipelines.Import
{
  using System.Data.SqlClient;
  using System.Linq;
  using System.Xml;
  using SIM.Adapters.WebServer;
  using SIM.Extensions;

  public class UpdateConnectionStrings : ImportProcessor
  {
    #region Protected methods

    protected override void Process(ImportArgs args)
    {
      var pathToConnectionStringsConfig = args._RootPath.PathCombine("Website").PathCombine("App_Config").PathCombine("ConnectionStrings.config");
      var connectionStringsDocument = new XmlDocumentEx();
      connectionStringsDocument.Load(pathToConnectionStringsConfig);
      var connectionsStringsElement = new XmlElementEx(connectionStringsDocument.DocumentElement, connectionStringsDocument);
      ConnectionStringCollection connStringCollection = this.GetConnectionStringCollection(connectionsStringsElement);

      foreach (var conn in connStringCollection)
      {
        if (conn.IsSqlConnectionString)
        {
          var builder = new SqlConnectionStringBuilder(conn.Value)
          {
            IntegratedSecurity = false, 
            DataSource = args._ConnectionString.DataSource, 
            UserID = args._ConnectionString.UserID, 
            Password = args._ConnectionString.Password
          };

          if (args._DatabaseNameAppend != -1)
          {
            builder.InitialCatalog = builder.InitialCatalog + "_" + args._DatabaseNameAppend.ToString();
          }
          else
          {
            builder.InitialCatalog = builder.InitialCatalog;
          }

          conn.Value = builder.ToString();
        }
      }

      connStringCollection.Save();
    }

    #endregion

    #region Private methods

    private ConnectionStringCollection GetConnectionStringCollection(XmlElementEx connectionStringsNode)
    {
      var connectionStrings = new ConnectionStringCollection(connectionStringsNode);
      XmlNodeList addNodes = connectionStringsNode.Element.ChildNodes;
      connectionStrings.AddRange(addNodes.OfType<XmlElement>().Select(element => new ConnectionString(element, connectionStringsNode.Document)));

      return connectionStrings;
    }

    #endregion
  }
}