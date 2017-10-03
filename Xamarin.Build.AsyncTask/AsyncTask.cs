using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

// Keep AssemblyVersion constant and pointing to the base version, which is the "release", so to speak.
[assembly: AssemblyVersion(ThisAssembly.Git.BaseVersion.Major + "." + ThisAssembly.Git.BaseVersion.Minor + "." + ThisAssembly.Git.BaseVersion.Patch)]
// FileVersion and InformationalVersion hold the "right" values but don't mess with fusion.
[assembly: AssemblyFileVersion(ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.SemVer.Patch)]
// Full metadata about branch and commit is always useful.
[assembly: AssemblyInformationalVersion(
  ThisAssembly.Git.SemVer.Major + "." +
  ThisAssembly.Git.SemVer.Minor + "." +
  ThisAssembly.Git.SemVer.Patch + "-" +
  ThisAssembly.Git.Branch + "+" +
  ThisAssembly.Git.Commit)]

namespace Xamarin.Build
{
    /// <summary>
    /// Base class for tasks that need long-running cancellable asynchronous tasks 
    /// that don't block the UI thread in the IDE.
    /// </summary>
    public abstract class AsyncTask : Task, ICancelableTask
    {
        readonly CancellationTokenSource tcs = new CancellationTokenSource();
        readonly Queue logMessageQueue = new Queue();
        readonly Queue warningMessageQueue = new Queue();
        readonly Queue errorMessageQueue = new Queue();

        readonly ManualResetEvent logDataAvailable = new ManualResetEvent(false);
        readonly ManualResetEvent errorDataAvailable = new ManualResetEvent(false);
        readonly ManualResetEvent warningDataAvailable = new ManualResetEvent(false);
        readonly ManualResetEvent taskCancelled = new ManualResetEvent(false);
        readonly ManualResetEvent completed = new ManualResetEvent(false);

        bool isRunning = true;
        object _eventlock = new object();
        int UIThreadId = 0;

        /// <summary>
        /// The cancellation token to notify the cancellation requests
        /// </summary>
        public CancellationToken Token { get { return tcs.Token; } }

        /// <summary>
        /// Indicates if the task will yield the node during tool execution.
        /// </summary>
        public bool YieldDuringToolExecution { get; set; }

        /// <summary>
        /// Initializes the task.
        /// </summary>
        public AsyncTask()
        {
            YieldDuringToolExecution = false;
            UIThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Requests cancellation of the task execution.
        /// </summary>
        public void Cancel()
        {
            taskCancelled.Set();
        }

        /// <summary>
        /// Completes the task execution.
        /// </summary>
        public void Complete()
        {
            completed.Set();
        }

        /// <summary>
        /// Waits until execution completes and returns <c>true</c> if no 
        /// errors were logged.
        /// </summary>
        public override bool Execute()
        {
            WaitForCompletion();
#pragma warning disable 618
            return !Log.HasLoggedErrors;
#pragma warning restore 618
        }

        [Obsolete("Do not use the Log.LogXXXX from within your Async task as it will Lock the Visual Studio UI. Use the this.LogXXXX methods instead.")]
        private new TaskLoggingHelper Log
        {
            get { return base.Log; }
        }

        public void LogDebugTaskItems(string message, string[] items)
        {
            LogDebugMessage(message);

            if (items == null)
                return;

            foreach (var item in items)
                LogDebugMessage("    {0}", item);
        }

        public void LogDebugTaskItems(string message, ITaskItem[] items)
        {
            LogDebugMessage(message);

            if (items == null)
                return;

            foreach (var item in items)
                LogDebugMessage("    {0}", item.ItemSpec);
        }

        public void LogMessage(string message)
        {
            LogMessage(message, importance: MessageImportance.Normal);
        }

        public void LogMessage(string message, params object[] messageArgs)
        {
            LogMessage(string.Format(message, messageArgs));
        }

        public void LogDebugMessage(string message)
        {
            LogMessage(message, importance: MessageImportance.Low);
        }

        public void LogDebugMessage(string message, params object[] messageArgs)
        {
            LogMessage(string.Format(message, messageArgs), importance: MessageImportance.Low);
        }

        public void LogMessage(string message, MessageImportance importance = MessageImportance.Normal)
        {
            if (UIThreadId == Thread.CurrentThread.ManagedThreadId)
            {
#pragma warning disable 618
                Log.LogMessage(importance, message);
                return;
#pragma warning restore 618
            }

            lock (logMessageQueue.SyncRoot)
            {
                logMessageQueue.Enqueue(new BuildMessageEventArgs(
                    message: message,
                    helpKeyword: null,
                    senderName: null,
                    importance: importance
                ));
                lock (_eventlock)
                {
                    if (isRunning)
                        logDataAvailable.Set();
                }
            }
        }

        public void LogError(string message)
        {
            LogError(code: null, message: message, file: null, lineNumber: 0);
        }

        public void LogError(string message, params object[] messageArgs)
        {
            LogError(code: null, message: string.Format(message, messageArgs));
        }

        public void LogErrorFromException(Exception exception)
        {
            if (UIThreadId == Thread.CurrentThread.ManagedThreadId)
            {
#pragma warning disable 618
                Log.LogErrorFromException(exception);
                return;
#pragma warning restore 618
            }

            StackFrame exceptionFrame = null;
            try
            {
                exceptionFrame = new StackTrace(exception, true)?.GetFrames()?.FirstOrDefault();
            }
            catch { }

            lock (errorMessageQueue.SyncRoot)
            {
                errorMessageQueue.Enqueue(new BuildErrorEventArgs(
                    subcategory: null,
                    code: null,
                    file: exceptionFrame?.GetFileName(),
                    lineNumber: exceptionFrame?.GetFileLineNumber() ?? 0,
                    columnNumber: exceptionFrame?.GetFileColumnNumber() ?? 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: exception.Message,
                    helpKeyword: null,
                    senderName: null
                ));
                lock (_eventlock)
                {
                    if (isRunning)
                        errorDataAvailable.Set();
                }
            }
        }

        public void LogCodedError(string code, string message)
        {
            LogError(code: code, message: message, file: null, lineNumber: 0);
        }

        public void LogCodedError(string code, string message, params object[] messageArgs)
        {
            LogError(code: code, message: string.Format(message, messageArgs), file: null, lineNumber: 0);
        }

        public void LogError(string code, string message, string file = null, int lineNumber = 0)
        {
            if (UIThreadId == Thread.CurrentThread.ManagedThreadId)
            {
#pragma warning disable 618
                Log.LogError(
                    subcategory: null,
                    errorCode: code,
                    helpKeyword: null,
                    file: file,
                    lineNumber: lineNumber,
                    columnNumber: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: message
                );
                return;
#pragma warning restore 618
            }

            lock (errorMessageQueue.SyncRoot)
            {
                errorMessageQueue.Enqueue(new BuildErrorEventArgs(
                    subcategory: null,
                    code: code,
                    file: file,
                    lineNumber: lineNumber,
                    columnNumber: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: message,
                    helpKeyword: null,
                    senderName: null
                ));
                lock (_eventlock)
                {
                    if (isRunning)
                        errorDataAvailable.Set();
                }
            }
        }

        public void LogWarning(string message, params object[] messageArgs)
        {
            LogWarning(string.Format(message, messageArgs));
        }

        public void LogWarning(string message)
        {
            if (UIThreadId == Thread.CurrentThread.ManagedThreadId)
            {
#pragma warning disable 618
                Log.LogWarning(message);
                return;
#pragma warning restore 618
            }

            lock (warningMessageQueue.SyncRoot)
            {
                warningMessageQueue.Enqueue(new BuildWarningEventArgs(
                    subcategory: null,
                    code: null,
                    file: null,
                    lineNumber: 0,
                    columnNumber: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: message,
                    helpKeyword: null,
                    senderName: null
                ));
                lock (_eventlock)
                {
                    if (isRunning)
                        warningDataAvailable.Set();
                }
            }
        }

        void LogMessages()
        {
            lock (logMessageQueue.SyncRoot)
            {
                while (logMessageQueue.Count > 0)
                {
                    var args = (BuildMessageEventArgs)logMessageQueue.Dequeue();
#pragma warning disable 618
                    Log.LogMessage(args.Importance, args.Message);
#pragma warning restore 618
                }
                logDataAvailable.Reset();
            }
        }

        void LogErrors()
        {
            lock (errorMessageQueue.SyncRoot)
            {
                while (errorMessageQueue.Count > 0)
                {
                    var args = (BuildErrorEventArgs)errorMessageQueue.Dequeue();
#pragma warning disable 618
                    Log.LogError(string.Empty, args.Code, string.Empty, args.File, args.LineNumber, 0, 0, 0, args.Message);
#pragma warning restore 618
                }
                errorDataAvailable.Reset();
            }
        }

        void LogWarnings()
        {
            lock (warningMessageQueue.SyncRoot)
            {
                while (warningMessageQueue.Count > 0)
                {
                    var args = (BuildWarningEventArgs)warningMessageQueue.Dequeue();
#pragma warning disable 618
                    Log.LogWarning(args.Message);
#pragma warning restore 618
                }
                warningDataAvailable.Reset();
            }
        }

        /// <summary>
        /// Waits for the task execution to complete.
        /// </summary>
        protected void WaitForCompletion()
        {
            var handles = new WaitHandle[]
            {
                logDataAvailable,
                errorDataAvailable,
                warningDataAvailable,
                taskCancelled,
                completed,
            };

            if (YieldDuringToolExecution && BuildEngine is IBuildEngine3)
                (BuildEngine as IBuildEngine3).Yield();

            try
            {
                while (isRunning)
                {
                    var index = (WaitHandleIndex)WaitHandle.WaitAny(handles, TimeSpan.FromMilliseconds(10));
                    switch (index)
                    {
                        case WaitHandleIndex.LogDataAvailable:
                            LogMessages();
                            break;
                        case WaitHandleIndex.ErrorDataAvailable:
                            LogErrors();
                            break;
                        case WaitHandleIndex.WarningDataAvailable:
                            LogWarnings();
                            break;
                        case WaitHandleIndex.TaskCancelled:
                            tcs.Cancel();
                            isRunning = false;
                            break;
                        case WaitHandleIndex.Completed:
                            isRunning = false;
                            break;
                    }
                }

            }
            finally
            {
                if (YieldDuringToolExecution && BuildEngine is IBuildEngine3)
                    (BuildEngine as IBuildEngine3).Reacquire();
            }
        }

        enum WaitHandleIndex
        {
            LogDataAvailable,
            ErrorDataAvailable,
            WarningDataAvailable,
            TaskCancelled,
            Completed,
        }
    }
}

