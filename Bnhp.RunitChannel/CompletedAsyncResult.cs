﻿namespace Bnhp.RunitChanel
{
  using System;
  using System.Threading;

  class CompletedAsyncResult: IAsyncResult
  {
    ManualResetEvent waitHandle = new ManualResetEvent(true);

    public TimeSpan Timeout { get; set; }

    public object AsyncState { get; set; }

    public WaitHandle AsyncWaitHandle
    {
      get { return this.waitHandle; }
    }

    public bool CompletedSynchronously
    {
      get { return true; }
    }

    public bool IsCompleted
    {
      get { return true; }
    }
  }
}
