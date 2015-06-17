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
using System.Reflection;

namespace Bphx.Tracers
{
  /// <summary>
  /// Defines a trace listener that writes trace to the event log.
  /// </summary>
  public class EventLogTraceListener : TraceListener
  {
    /// <summary>
    /// Default constructor.
    /// </summary>
    public EventLogTraceListener()
    {
      traceLevel = TraceLevel.Info;
    }

    /// <summary>
    /// Creates a DatabaseTraceListener instance.
    /// </summary>
    /// <param name="traceLevel">a TraceLevel for the trace listener.</param>
    public EventLogTraceListener(string traceLevel)
    {
      this.traceLevel = (TraceLevel)Enum.Parse(typeof(TraceLevel), traceLevel);
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
        WriteToEventLog(severity, message);
      }
    }

    /// <summary>
    /// Writes a message or the value of an object's to the listener.
    /// </summary>
    /// <param name="message">a message to wrtire to the listener.</param>
    public override void Write(string message)
    {
      WriteToEventLog(TraceEventType.Information, message);
    }

    /// <summary>
    /// Writes a message or the value of an object's to the listener 
    /// followed by a line terminator.
    /// </summary>
    /// <param name="message">a message to wrtire to the listener.</param>
    public override void WriteLine(string message)
    {
      WriteToEventLog(TraceEventType.Information, message);
    }

    /// <summary>
    /// Closes the trace listner so it no longer receives tracing output.
    /// </summary>
    public override void Close()
    {
      if (eventLog != null)
      {
        eventLog.Close();

        eventLog = null;
      }
    }

    /// <summary>
    ///  Releases the resources used by the trace listener.
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        Close();
      }
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
    /// <param name="severity">
    /// One of the TraceEventType values specifying the type of event that 
    /// has caused the trace.
    /// </param>
    /// <param name="message">a message to write.</param>
    private void WriteToEventLog(
      TraceEventType severity,
      string message)
    {
      try
      {
        var match = ParseErrorMessage.Match(message);
        var category = match.Success ?
          GetEventTypeID(match.Groups["category"].Value) :
          UNKNOWN_CATEGORY_ID;
        var text = match.Success ? match.Groups["message"].Value : message;
        var instance = new EventInstance(category, 0);

        switch (severity)
        {
          case TraceEventType.Critical:
          case TraceEventType.Error:
          {
            instance.EntryType = EventLogEntryType.Error;

            break;
          }
          case TraceEventType.Warning:
          {
            instance.EntryType = EventLogEntryType.Warning;

            break;
          }
          default:
          {
            instance.EntryType = EventLogEntryType.Information;

            break;
          }
        }

        var eventLog = this.EventLog;

        if (eventLog != null)
        {
          eventLog.WriteEvent(instance, new object[] { text });
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
        // we have to catch internal errors when we write message to trace.
      }
    }

    /// <summary>
    /// Gets an event log instance.
    /// </summary>
    private EventLog EventLog
    {
      get
      {
        if (this.eventLog == null)
        {
          try
          {
            var eventLog = new EventLog();

            eventLog.Source = Path.GetFileNameWithoutExtension(
              Assembly.GetEntryAssembly().Location);

            this.eventLog = eventLog;
          }
          catch
          {
            // use default source name.
          }
        }

        return this.eventLog;
      }
    }

    /// <summary>
    /// Defines an unknown category ID.
    /// </summary>
    private const int UNKNOWN_CATEGORY_ID = 1000;

    /// <summary>
    /// Retrieves a category name , if any, from error message.
    /// </summary>
    private static Regex ParseErrorMessage = new Regex(
      "^(?<category>[^:]+):\\s*(?<message>.+)$",
      RegexOptions.IgnoreCase |
      RegexOptions.CultureInvariant |
      RegexOptions.Multiline);

    private TraceLevel traceLevel;
    private EventLog eventLog;
  }
}
