namespace Bphx.Tracers
{
  using System;
  using System.Linq;
  using System.Web;
  using System.Net;
  using System.ServiceModel.Dispatcher;
  using System.ServiceModel.Channels;
  using System.Collections.Generic;
  using System.Runtime.Serialization.Json;
  using System.ServiceModel;
  using System.Data;
  using System.Reflection;
  using System.Configuration;
  using System.Diagnostics;

  /// <summary>
  /// JSON error handler.
  /// See details at http://www.nesterovsky-bros.com/weblog/2013/05/13/ErrorHandlingInWCFBasedWebApplications.aspx
  /// </summary>
  public class JsonErrorHandler : IErrorHandler
  {
    public bool HandleError(Exception error)
    {
      // Yes, we handled this exception...
      return true;
    }

    public void ProvideFault(
      Exception error, 
      MessageVersion version, 
      ref System.ServiceModel.Channels.Message fault)
    {
      // Modify response
      var rmp = new HttpResponseMessageProperty
      {
        StatusCode = HttpStatusCode.BadRequest,
        StatusDescription = "Bad Request",
      };

      rmp.Headers[HttpResponseHeader.ContentType] = "application/json";

      // Create message
      var exceptionStackTrace = Convert.ToBoolean(
        ConfigurationManager.AppSettings["ExceptionStackTrace"]);

      var jsonError = CreateErrorDetails(error, exceptionStackTrace);

      fault = System.ServiceModel.Channels.Message.CreateMessage(
        version,
        "",
        jsonError,
        new DataContractJsonSerializer(typeof(JsonErrorDetails)));

      // Tell WCF to use JSON encoding rather than default XML
      fault.Properties.Add(WebBodyFormatMessageProperty.Name,
        new WebBodyFormatMessageProperty(WebContentFormat.Json));
      fault.Properties.Add(HttpResponseMessageProperty.Name, rmp);
    }

    public static JsonErrorDetails CreateErrorDetails(
      Exception error, 
      bool includeStackTrace)
    {
      if (error == null)
      {
        return null;
      }

      return new JsonErrorDetails
      {
        Message = error.Message,
        ExceptionType = error.GetType().FullName,
        InnerException = CreateErrorDetails(error.InnerException, includeStackTrace),
        StackTrace = includeStackTrace ? error.ToString() : null,
        ErrorID = DateTime.Now.Ticks
      };
    }
  }

  /// <summary>
  /// An exception wrapper.
  /// </summary>
  public class JsonErrorDetails
  {
    /// <summary>
    /// Gets and sets the error code.
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// Gets and sets the error message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets and sets an origin exception type.
    /// </summary>
    public string ExceptionType { get; set; }

    /// <summary>
    /// Gets and sets an inner exception details, if any.
    /// </summary>
    public JsonErrorDetails InnerException { get; set; }

    /// <summary>
    /// Gets and sets an exception stack trace.
    /// </summary>
    public string StackTrace { get; set; }

    /// <summary>
    /// Gets and sets error ID.
    /// </summary>
    public long? ErrorID { get; set; }
  }
}