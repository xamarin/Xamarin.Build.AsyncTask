using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Threading;
using System.Collections;

namespace Xamarin.Build
{
    public class CancelableTask : Task, ICancelableTask
    {
        CancellationTokenSource tcs = new CancellationTokenSource();

        /// <summary>
        /// The cancellation token to notify the cancellation requests
        /// </summary>
        public CancellationToken Token { get { return tcs.Token; } }

        /// <summary>
        /// Requests cancellation of the task execution.
        /// </summary>
        public virtual void Cancel()
        {
            tcs.Cancel();
        }

        public override bool Execute()
        {
            return !Log.HasLoggedErrors;
        }
    }
}