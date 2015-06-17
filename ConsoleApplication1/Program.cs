using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Exchange.WebServices.Data;

using Multiconn.Experanto.Serializer;

namespace ConsoleApplication1
{
  class Program
  {
    static void Main(string[] args)
    {
      try
      {
        var owner = "anesterovsky@modernsystems.com";
        var password = "y8Tqpg(5)";
        //var service1 = Connect(owner, password);
        var now = DateTime.Now;
        var ID = "";

        var service = new ExchangeService(ExchangeVersion.Exchange2013);

        service.Credentials = new WebCredentials(owner, password);
        service.UseDefaultCredentials = false;
        service.PreAuthenticate = true;
        service.Url = 
          new Uri("https://outlook.office365.com/EWS/Exchange.asmx");
          //service1.Url;
        
        //SendEMail(
        //  service, 
        //  "BOMB-анд/и-ровщик", 
        //  "Тест клиента - бомбандировщика. Готовся...", 
        //  "vnesterovsky@modernsystems.com");

        //if (owner.StartsWith("anesterovsky"))
        if (false)
        {
          var meeting = AppointMeeting(
            service,
            "Test Office 365 API (" + DateTime.Now.Date.ToShortDateString() + ")",
            "Let's discuss append a meeting to a calendar in Office 365 from C# application.",
            now.AddMinutes(10),
            now.AddMinutes(15),
            "Near the Shabtay's place",
            5,
            "vnesterovsky@modernsystems.com",
            "anesterovsky@modernsystems.com"
            //,"tamir.ben-shoshan@poalim.co.il"
            );

          ID = meeting.Id.ToString();
        }
        else
        {
          // ItemSchema.Id:
          //ID = "AAMkAGUzNmExZTQ0LTgyNzAtNGQ4My1hYWI3LTA0OWEyOWEw" +
          //  "MDlmZQBGAAAAAADmooDQi68AQogHBMGEp1b/BwDjZk4p1JMgSp0Bp/W+" +
          //  "Cvy7AAAABYxqAAAtyRrCObzRQaG/zOq36q3MAAAm9stEAAA=";

          // AppointmentSchema.ICalUid:
          ID = "040000008200E00074C5B7101A82E008000000001663FAF89AA2D00" +
            "1000000000000000010000000F40453CB06FF494B8BFDEBBB37205848";
          //ID = "dlbhkgko5go1ektrkag18jqos4@google.com";
        }

        Console.WriteLine("List of appointments for last 10 days:");
        PrintAppointments(service, now.AddDays(-10), now.AddDays(10), 100);
        Console.WriteLine();

        var appointment = FindAppointment(service, ID);

        if (appointment != null)
        {
          Console.WriteLine("Found an appointment:");

          PrintAppointment(appointment);

          UpdateAppointmentTime(appointment, now.AddMinutes(10));

          Console.WriteLine("List of appointments for last 10 days:");
          PrintAppointments(service, now.AddDays(-10), now.AddDays(10), 100);
          Console.WriteLine();
        }
        else
        {
          Console.WriteLine("No an appointment was found for ID: " + ID);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e.ToString());
      }

      Console.WriteLine("Press enter to exit...");
      Console.ReadLine();
    }

    private static void PrintAppointment(Appointment appoitment)
    {
      Console.WriteLine("ID: " + appoitment.Id);
      Console.WriteLine("UID: " + appoitment.ICalUid);
      Console.WriteLine("Subject: " + appoitment.Subject);
      Console.WriteLine("Location: " + appoitment.Location);
      Console.WriteLine("Start: " + appoitment.Start);
      Console.WriteLine("End: " + appoitment.End);
      Console.WriteLine("Attendees: " + appoitment.DisplayTo);

      var boolValue = false;

      appoitment.TryGetProperty(AppointmentSchema.IsMeeting, out boolValue);

      Console.WriteLine("Is meeting: " + boolValue);

      boolValue = false;

      appoitment.TryGetProperty(AppointmentSchema.IsRecurring, out boolValue);

      Console.WriteLine("Is recurring: " + boolValue);

      if (boolValue)
      {
        var recurrence = null as Recurrence;

        if (appoitment.TryGetProperty(AppointmentSchema.Recurrence, out recurrence))
        {
          Console.WriteLine("  Start date: " + recurrence.StartDate);

          if (appoitment.Recurrence.HasEnd)
          {
            Console.WriteLine("  End date: " + recurrence.EndDate);
          }
          else
          {
            Console.WriteLine("  Never end.");
          }
        }
      }
      
      Console.WriteLine("================================================");
      Console.WriteLine();
    }

    private static Appointment FindAppointment(ExchangeService service, string searchFor)
    {
      //var filter = new SearchFilter.IsEqualTo(ItemSchema.Id, searchFor);
      //var filter = new SearchFilter.IsEqualTo(AppointmentSchema.ICalUid, searchFor);

      var property = new ExtendedPropertyDefinition(
        DefaultExtendedPropertySet.Meeting,
        0x23,
        MapiPropertyType.Binary);
      var value =
        Convert.ToBase64String(HexEncoder.HexStringToArray(searchFor));
      var filter = new SearchFilter.IsEqualTo(property, value);

      // Limit the result set to 10 items.
      ItemView view = new ItemView(10);

      //view.PropertySet = new PropertySet(
      //  AppointmentSchema.Subject,
      //  AppointmentSchema.Start,
      //  AppointmentSchema.End,
      //  AppointmentSchema.Location,
      //  AppointmentSchema.ICalUid);

      // Item searches do not support Deep traversal.
      view.Traversal = ItemTraversal.Shallow;

      FindItemsResults<Item> appointments = service.FindItems(
        WellKnownFolderName.Calendar, 
        filter, 
        view);

      if (appointments != null)
      {
        return appointments.FirstOrDefault() as Appointment;
      }

      return null;
    }

    private static void PrintAppointments(
      ExchangeService service,
      DateTime from,
      DateTime to,
      int top = 10)
    {
      // Initialize the calendar folder object with only the folder ID. 
      var calendar = CalendarFolder.Bind(
        service, 
        WellKnownFolderName.Calendar, 
        new PropertySet());

      // Set the start and end time and number of appointments to retrieve.
      var view = new CalendarView(from, to);

      // Limit the properties returned to the appointment's subject, start time, and end time.
      view.PropertySet = new PropertySet(
        AppointmentSchema.Subject, 
        AppointmentSchema.Start, 
        AppointmentSchema.End,
        AppointmentSchema.Location,
        AppointmentSchema.ICalUid,
        AppointmentSchema.DisplayTo);

      // Retrieve a collection of appointments by using the calendar view.
      var appointments = calendar.FindAppointments(view);

      Console.WriteLine(
        "\nFound " + appointments.Count() + 
        " appointments on your calendar from " + from.ToShortDateString() +
        " to " + to.ToShortDateString() + ": \n");

      foreach (Appointment a in appointments)
      {
        PrintAppointment(a);
      }
    }

    private static Appointment AppointMeeting(
      ExchangeService service,
      string subject,
      string message,
      DateTime start,
      DateTime end,
      string location,
      int reminderBefore,
      params string[] attendees)
    {
      var meeting = new Appointment(service);

      // Set the properties on the meeting object to create the meeting.
      meeting.Subject = subject;
      meeting.Body = message;
      meeting.Start = start;
      meeting.End = end;
      meeting.Location = location;

      foreach (var attendee in attendees)
      {
        meeting.RequiredAttendees.Add(attendee);
      }
      
      //meeting.RequiredAttendees.Add("Sadie.Daniels@contoso.com");
      //meeting.OptionalAttendees.Add("Magdalena.Kemp@contoso.com");
      
      //meeting.ReminderMinutesBeforeStart = reminderBefore;
      //meeting.Recurrence = new Recurrence.DailyPattern(start, 2);


      // Send the meeting request
      meeting.Save(SendInvitationsMode.SendToAllAndSaveCopy);

      return meeting;
    }

    private static void UpdateAppointmentTime(
      Appointment appointment, 
      DateTime start, 
      DateTime? end = null)
    {
      // Instantiate an appointment object by binding to it by using the ItemId.
      // As a best practice, limit the properties returned to only the ones you need.
      //Appointment appointment = Appointment.Bind(service, appointmentId, new PropertySet(AppointmentSchema.Subject, AppointmentSchema.Start, AppointmentSchema.End));

      var time = appointment.End - appointment.Start;

      appointment.Start = start;

      if (end.HasValue)
      {
        appointment.End = end.Value;
      }
      else
      {
        appointment.End = start + time;
      }

      // Unless explicitly specified, the default is to use SendToAllAndSaveCopy.
      // This can convert an appointment into a meeting. To avoid this,
      // explicitly set SendToNone on non-meetings.
      var mode = appointment.IsMeeting ?
        SendInvitationsOrCancellationsMode.SendToAllAndSaveCopy : 
        SendInvitationsOrCancellationsMode.SendToNone;

      appointment.Update(ConflictResolutionMode.AlwaysOverwrite, mode);
    }

    private static void SendEMail(
      ExchangeService service, 
      string subject, 
      string message, 
      params string[] recipients)
    {
      EmailMessage email = new EmailMessage(service);

      foreach (var recipient in recipients)
      {
        email.ToRecipients.Add(recipient);
      }
      
      email.Subject = subject;
      email.Body = new MessageBody(message);

      email.Send();
    }

    private static ExchangeService Connect(string user, string password)
    {
      var service = new ExchangeService(ExchangeVersion.Exchange2013);

      service.Credentials = new WebCredentials(user, password);
      service.UseDefaultCredentials = false;

      //service.TraceEnabled = true;
      //service.TraceFlags = TraceFlags.All;

      service.AutodiscoverUrl(user, RedirectionUrlValidationCallback);

      return service;
    }

    private static bool RedirectionUrlValidationCallback(string redirectionUrl)
    {
      return redirectionUrl.StartsWith("https");
      
      //// The default for the validation callback is to reject the URL.
      //bool result = false;

      //Uri redirectionUri = new Uri(redirectionUrl);

      //// Validate the contents of the redirection URL. In this simple validation
      //// callback, the redirection URL is considered valid if it is using HTTPS
      //// to encrypt the authentication credentials. 
      //if (redirectionUri.Scheme == "https")
      //{
      //  result = true;
      //}

      //return result;
    }
  }
}
