using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Threading;
using System.Collections.Generic;
using TPL = System.Threading.Tasks;

namespace Xamarin.Build
{
    /// <summary>
    /// This Task will wait for all the background tasks to complete.
    /// You can control which task categories to wait on by using the
    /// `Categories` property.
    /// </summary>
    public class WaitForBackgroundTasks : AsyncTask
    {
        internal static readonly string [] DefaultCategories = { BackgroundTaskManager.DefaultCategory };
        /// <summary>
        /// A list of background task categories to wait on.
        /// Once all the tasks in each of these categories have Completed
        /// the task will exit.
        /// By default it will wait on the `default` category.
        /// </summary>
        public string [] Categories { get; set; }

        /// <summary>
        /// The error code to use should any of the background tasks fail.
        /// this will default to XAT0000
        /// </summary>
        public string ErrorCode { get; set; } = "XAT0000";

        public override bool Execute ()
        {
            var manager = BackgroundTaskManager.GetTaskManager (BuildEngine4);
            List<AsyncTask> tasks = new List<AsyncTask> ();
            if (manager == null || manager.Count == 0)
            {
                Log.LogMessage (MessageImportance.Normal, $"No tasks found in TaskManager");
                return true;
            }
            foreach (string category in Categories ?? DefaultCategories)
            {
                if (manager [category].Length == 0)
                {
                    Log.LogMessage (MessageImportance.Normal, $"Not tasks found for {category}");
                    continue;
                }
                else
                {
                    Log.LogMessage (MessageImportance.Normal, $"Waiting on Tasks {category}");
                    tasks.AddRange (manager [category]);
                }
            }
            if (tasks.Count == 0)
            {
                Log.LogMessage (MessageImportance.Normal, $"No Tasks found for Categories [{string.Join (",", Categories)}]");
                return true;
            }
            // We need to wait for the completion of any background tasks
            // needs to be done on the UI Thread so we get correct logging.
            foreach (var task in tasks)
            {
                Log.LogMessage (MessageImportance.Normal, $"Waiting on {task.GetType ()}");
                task.Wait (BuildEngine);
            }
            Complete ();
            base.Execute ();
            Log.LogMessage (MessageImportance.Normal, $"All Tasks in Categories [{string.Join (",", Categories)}] have Completed.");
            return !Log.HasLoggedErrors;
        }
    }
}
