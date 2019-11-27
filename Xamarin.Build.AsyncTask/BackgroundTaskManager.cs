using System;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Collections;
using TPL = System.Threading.Tasks;

namespace Xamarin.Build
{
    /// <summary>
    /// A class for keeping track of any Task instances which are run as part of
    /// a build.
    /// Call `manager.RegisterTask` to register the task with the manager. You can
    /// provide a `Category` for each task. This is so they can be waited on later
    /// using the `WaitForBackgroundTasks` MSBuild task.
    /// </summary>
    public class BackgroundTaskManager : IDisposable
    {
        internal const string DefaultCategory = "default";
        ConcurrentDictionary<string, ConcurrentBag<AsyncTask>> tasks = new ConcurrentDictionary<string, ConcurrentBag<AsyncTask>> ();
        CancellationTokenSource tcs = new CancellationTokenSource ();
        IBuildEngine4 buildEngine;

        /// <summary>
        /// Get an Instance of the TaskManager.
        /// NOTE This MUST be called from the main thread in a Task, it cannot be called on a background thread.
        /// </summary>
        /// <param name="buildEngine4">An instance of the IBuildEngine4 interface.</param>
        /// <returns>An instance of a TaskManager</returns>
        public static BackgroundTaskManager GetTaskManager (IBuildEngine4 buildEngine4)
        {
            var manager = (BackgroundTaskManager)buildEngine4.GetRegisteredTaskObject (typeof (BackgroundTaskManager).FullName, RegisteredTaskObjectLifetime.Build);
            if (manager == null)
            {
                manager = new BackgroundTaskManager (buildEngine4);
                buildEngine4.RegisterTaskObject (typeof (BackgroundTaskManager).FullName, manager, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
            }
            return manager;
        }

        public BackgroundTaskManager(IBuildEngine4 buildEngine)
        {
            this.buildEngine = buildEngine;
        }

        /// <summary>
        /// Register a new Task which will be running in the background.
        /// If you have multiple tasks running you can split them up into
        /// different categories. This can then we used to wait in differnt
        /// parts of the build later on.
        /// </summary>
        /// <param name="task">The task you are running</param>
        /// <param name="category">The category this task is in. </param>
        public void RegisterTask (AsyncTask task, string category = DefaultCategory)
        {
            var bag = tasks.GetOrAdd (category, new ConcurrentBag<AsyncTask> ());
            bag.Add (task);
        }

        /// <summary>
        /// Returns an array of Tasks for that category.
        /// </summary>
        /// <param name="category">The category you want to get the list for.</param>
        /// <returns>Either the array of task or an empty array if the category does not exist.</returns>
        public AsyncTask [] this [string category]
        {
            get
            {
                ConcurrentBag<AsyncTask> result;
                if (!tasks.TryGetValue (category, out result))
                    return Array.Empty<AsyncTask> ();
                return result.ToArray ();
            }
        }

        public void Dispose ()
        {
            // wait for all tasks to complete.
            foreach (var bag in tasks) {
                foreach (AsyncTask t in bag.Value) {
                    t.Wait (buildEngine);
                }
            }
            tcs.Cancel ();
        }

        public CancellationToken CancellationToken { get { return tcs.Token; } }

        /// <summary>
        /// The number of registered categories.
        /// </summary>
        public int Count => tasks.Count;
    }
}
