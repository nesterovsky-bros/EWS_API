using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Configuration;
using System.Text;
using System.Text.RegularExpressions;

namespace Bphx.Tracers
{
  /// <summary>
  /// Defines a trace listener that writes trace to the reports' database.
  /// </summary>
  public class DatabaseTraceListener: TraceListener
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public DatabaseTraceListener()
    {
      this.traceLevel = TraceLevel.Info;
    }

    /// <summary>
    /// Creates a DatabaseTraceListener instance.
    /// </summary>
    /// <param name="traceLevel">a TraceLevel for the trace listener.</param>
    public DatabaseTraceListener(string traceLevel)
    {
      this.traceLevel = (TraceLevel)Enum.Parse(typeof(TraceLevel), traceLevel);
    }

    /// <summary>
    /// Gets a machine name.
    /// </summary>
    public string MachineName
    {
      get
      {
        if (machineName == null)
        {
          try
          {
            machineName = Environment.MachineName;
          }
          catch
          {
            machineName = "";
          }
        }

        return machineName;
      }
    }

    /// <summary>
    /// Writes trace information, a message, and event information to 
    /// the listener specific output.
    /// </summary>
    /// <param name="eventCache">
    /// A TraceEventCache object that contains the current process ID, 
    /// thread ID, and stack trace information.
    /// </param>
    /// <param name="source">
    /// A name used to identify the output, typically the name of the 
    /// application that generated the trace event.
    /// </param>
    /// <param name="severity">
    /// One of the TraceEventType values specifying the type of event that 
    /// has caused the trace.
    /// </param>
    /// <param name="id">A numeric identifier for the event.</param>
    /// <param name="message">
    /// A message to write.
    /// </param>
    public override void TraceEvent(
      TraceEventCache eventCache, 
      string source, 
      TraceEventType severity, 
      int id, 
      string message)
    {
      TraceEvent(eventCache, source, severity, id, message, null);
    }

    /// <summary>
    /// Writes trace information, a formatted array of objects and event 
    /// information to the listener specific output.
    /// </summary>
    /// <param name="eventCache">
    /// A TraceEventCache object that contains the current process ID, 
    /// thread ID, and stack trace information.
    /// </param>
    /// <param name="source">
    /// A name used to identify the output, typically the name of the 
    /// application that generated the trace event.
    /// </param>
    /// <param name="severity">
    /// One of the TraceEventType values specifying the type of event that 
    /// has caused the trace.
    /// </param>
    /// <param name="id">A numeric identifier for the event.</param>
    /// <param name="format">
    /// A format string that contains zero or more format items, which 
    /// correspond to objects in the args array.
    /// </param>
    /// <param name="args">
    /// An object array containing zero or more objects to format.
    /// </param>
    public override void TraceEvent(
      TraceEventCache eventCache, 
      string source, 
      TraceEventType severity, 
      int id, 
      string format, 
      params object[] args)
    {
      if (traceLevel == TraceLevel.Off)
      {
        return;
      }

      var message = args == null ? format : string.Format(format, args);

      if (!string.IsNullOrEmpty(MachineName))
      {
        var pos = string.IsNullOrEmpty(message) ? -1 : message.IndexOf(':');

        if (pos > 0)
        {
          message = message.Substring(0, pos) + ":" + 
            MachineName + ":" + 
            message.Substring(pos + 1);
        }
      }

      if ((
            (TraceEventType.Error >= severity) && 
            (TraceLevel.Error <= traceLevel)
          ) ||
          (
            (TraceEventType.Warning == severity) && 
            (TraceLevel.Warning <= traceLevel)
          ) ||
          (
            (TraceEventType.Information == severity) && 
            (TraceLevel.Info <= traceLevel)
          ))
      {
        WriteToDatabase(message);
      }
    }

    /// <summary>
    /// Writes a message or the value of an object's to the listener.
    /// </summary>
    /// <param name="message">a message to wrtire to the listener.</param>
    public override void Write(string message)
    {
      WriteToDatabase(message);
    }

    /// <summary>
    /// Writes a message or the value of an object's to the listener 
    /// followed by a line terminator.
    /// </summary>
    /// <param name="message">a message to wrtire to the listener.</param>
    public override void WriteLine(string message)
    {
      WriteToDatabase(message);
    }

    /// <summary>
    /// Converts a category name to an event type ID.
    /// </summary>
    /// <param name="category">a category name to convert.</param>
    /// <returns>an event type ID.</returns>
    private static int GetEventTypeID(string category)
    {
      var categoryID = UNKNOWN_CATEGORY_ID;

      if (category != null)
      {
        switch (category.ToUpper())
        {
          case "ERROR":
          {
            return categoryID + 1;
          }
          case "CONFIG":
          {
            return categoryID + 2;
          }
          case "WARNING":
          {
            return categoryID + 3;
          }
          case "INFO":
          {
            return categoryID + 4;
          }
        }
      }

      return categoryID;
    }

    /// <summary>
    /// Writes a trace message to the database.
    /// </summary>
    /// <param name="message">a message to write.</param>
    private static void WriteToDatabase(string message)
    {
      try
      {
        var match = RetrieveCategoryNameFromErrorMessage.Match(message);
        var category = match.Success ?
          GetEventTypeID(match.Groups["category"].Value) :
          UNKNOWN_CATEGORY_ID;

        match = RetrieveNameFromErrorMessage.Match(message);

        var file = 
          match.Success ? match.Groups["name"].Captures[0].Value : "";

        using (var connection = CreateConnection())
        using (var command = connection.CreateCommand())
        {
          command.CommandType = CommandType.StoredProcedure;

          // TODO: get a SP namespace and name from configuration.
          command.CommandText = "Load.Trace";

          command.Parameters.AddWithValue("@eventTypeID", category);
          command.Parameters.AddWithValue("@data", file);
          command.Parameters.AddWithValue("@description", message);

          connection.Open();
          command.ExecuteNonQuery();
        }
      }
      catch
      {
        // do nothing in case of error that occurs on write to database.
      }
    }

    /// <summary>
    /// Creates a database connection.
    /// </summary>
    /// <returns></returns>
    private static SqlConnection CreateConnection()
    {
      if (dbFactory == null)
      {
        var definition =
          ConfigurationManager.ConnectionStrings["DatabaseTracer"];

        if (definition == null)
        {
          throw new ApplicationException(
            "No database connection definition is found.");
        }

        dbFactory = DbProviderFactories.GetFactory(definition.ProviderName);
        connectionString = definition.ConnectionString;
      }

      var connection = (SqlConnection)dbFactory.CreateConnection();

      connection.ConnectionString = connectionString;

      return connection;
    }

    /// <summary>
    /// A db connection factory.
    /// </summary>
    private static DbProviderFactory dbFactory;

    /// <summary>
    /// A database connection string.
    /// </summary>
    private static string connectionString;

    /// <summary>
    /// Defines an unknown category ID.
    /// </summary>
    private const int UNKNOWN_CATEGORY_ID = 1000;

    /// <summary>
    /// Retrieves a category name , if any, from error message.
    /// </summary>
    private static Regex RetrieveCategoryNameFromErrorMessage = new Regex(
      "^(?<category>[^:]+):\\s*(.+)$",
      RegexOptions.IgnoreCase | 
      RegexOptions.CultureInvariant | 
      RegexOptions.Multiline);

    /// <summary>
    /// Retrieves a file or folder name, if any, from error message.
    /// </summary>
    private static Regex RetrieveNameFromErrorMessage = new Regex(
      "(file|folder) \"(?<name>[^\"]+)\"",         
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private TraceLevel traceLevel;
    private string machineName;
  }
}
