namespace Bnhp.Office365
{
  using System;
  using System.Diagnostics;
  using System.Text.RegularExpressions;
  using System.Threading.Tasks;

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

      if (((TraceEventType.Error >= severity) && 
        (TraceLevel.Error <= traceLevel)) ||
        ((TraceEventType.Warning == severity) && 
          (TraceLevel.Warning <= traceLevel)) ||
        ((TraceEventType.Information == severity) && 
          (TraceLevel.Info <= traceLevel)))
      {
        WriteToDatabase(severity + ": " + message);
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
    /// Writes a trace message to the database.
    /// </summary>
    /// <param name="message">a message to write.</param>
    private void WriteToDatabase(string message)
    {
      try
      {
        if (message == null)
        {
          message = "";
        }

        var details = message;
        var match = RetrieveCategoryNameFromErrorMessage.Match(message);
        var categoryGroup = match.Success ? match.Groups["category"] : null;
        var category = categoryGroup != null ? categoryGroup.Value : "";
        var start = 0;
        
        if (categoryGroup != null)
        {
          start = categoryGroup.Index + categoryGroup.Length + 1;
        }

        var end = message.IndexOf(". ");

        message = end != -1 ? message.Substring(start, end - start + 1) :
          start > 0 ? message.Substring(start) :
          message;

        if ((end == -1) || (end + 1 == message.Length))
        {
          details = null;
        }

        // NOTE: Run and forget the task.
        var task = 
          TraceIntoDatabase(DateTime.Now, category, message.Trim(), details);
      }
      catch
      {
        // do nothing in case of error that occurs on write to database.
      }
    }

    /// <summary>
    /// Saves a trace info into the database.
    /// </summary>
    /// <param name="timestamp">A trace timestamp.</param>
    /// <param name="category">A category.</param>
    /// <param name="message">A message.</param>
    /// <param name="details">A message details.</param>
    /// <returns>A task that logs data.</returns>
    private async Task TraceIntoDatabase(
      DateTime timestamp,
      string category, 
      string message, 
      string details)
    {
      category = category ?? "";

      if (category.Length > 32)
      {
        category = category.Substring(0, 32);
      }

      if ((message != null) && (message.Length > 1024))
      {
        message = message.Substring(0, 1024);
      }

      using(var model = new EWSQueueEntities())
      {
        model.TraceMessages.Add(
          new TraceMessage
          {
            Timestamp = timestamp,
            Type = category,
            Message = message,
            Details = details
          });

        // Run and forget.
        await model.SaveChangesAsync();
      }
    }

    /// <summary>
    /// Retrieves a category name , if any, from error message.
    /// </summary>
    private static Regex RetrieveCategoryNameFromErrorMessage = new Regex(
      "^\\s*(?<category>[^\\s:]+){1,32}:",
      RegexOptions.IgnoreCase | 
      RegexOptions.CultureInvariant | 
      RegexOptions.Multiline);

    private TraceLevel traceLevel;
  }
}
