﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ESServiceTest
{
  using System;
  using System.Collections.Generic;
  using System.Text;
  using Microsoft.VisualStudio.TestTools.WebTesting;

  [DeploymentItem("esservicetest\\App_Data\\TestData.mdf", "esservicetest\\App_Data")]
  [DataSource("DataSource", "System.Data.SqlClient", "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\\esservicetest" +
      "\\App_Data\\TestData.mdf;Integrated Security=True;Connect Timeout=30", Microsoft.VisualStudio.TestTools.WebTesting.DataBindingAccessMethod.Sequential, Microsoft.VisualStudio.TestTools.WebTesting.DataBindingSelectColumns.SelectOnlyBoundColumns, "CreateAndUpdateAppointment")]
  [DataBinding("DataSource", "CreateAndUpdateAppointment", "Email", "$Email")]
  [DataBinding("DataSource", "CreateAndUpdateAppointment", "RequiredAttendee", "$RequiredAttendee")]
  [DataBinding("DataSource", "CreateAndUpdateAppointment", "AppointmentDate", "$AppointmentDate")]
  [DataBinding("DataSource", "CreateAndUpdateAppointment", "AppointmentDate", "$AppointmentStartDate")]
  [DataBinding("DataSource", "CreateAndUpdateAppointment", "AppointmentDate", "$AppointmentEndDate")]
  [DataBinding("DataSource", "CreateAndUpdateAppointment", "AppointmentSubject", "$AppointmentSubject")]
  [DataBinding("DataSource", "CreateAndUpdateAppointment", "Text", "$Text")]
  public class CUDAppointmentSetupCoded : WebTest
  {
    public override IEnumerator<WebTestRequest> GetRequestEnumerator()
    {
      yield break;
    }
  }
}
