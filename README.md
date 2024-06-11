# Xamarin.Build.AsyncTask

Provides the AsyncTask to streamline the creation of long-running tasks that 
are cancellable and don't block the UI thread in Visual Studio. It also 
provides a set of Tasks and classes to start a long-running Task but 
wait for it to complete later in the build process. 

## Building

```
msbuild /t:restore && msbuild
```

That's it.

## Installation

```
install-package Xamarin.Build.AsyncTask
```

If you're creating a custom task library, just inherit from Xamarin.Build.AsyncTask.

If you're creating an inline task that wants to inherit from AsyncTask, use the following 
as a template:

```xml
  <UsingTask TaskName="MyAsyncTask" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
    <ParameterGroup>
      <!-- TODO: your task parameters -->
    </ParameterGroup>
    <Task>
      <Reference Include="$(AsyncTask)" />
      <Reference Include="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"/>
      <Reference Include="$(MSBuildToolsPath)\Microsoft.Build.Utilities.Core.dll"/>
      <Code Type="Class" Language="cs">
        <![CDATA[
        public class MyAsyncTask : Xamarin.Build.AsyncTask
        {
          public override bool Execute()
          {
            System.Threading.Tasks.Task.Run(async () =>
            {
              // TODO: do something long-running
              await System.Threading.Tasks.Task.Delay(5000);

              // Invoke Complete to signal you're done.
	            Complete();
            });

            return base.Execute();
          }
        }
        ]]>
      </Code>
    </Task>
  </UsingTask>
```

## CI Builds

Building and pushing from [VSTS](https://devdiv.visualstudio.com/DevDiv/XamarinVS/_build/index?context=allDefinitions&path=%5CXamarin&definitionId=7445&_a=completed).


## Running a Background Task

If you want to start a background task and continue with the build
and then wait for that background task later, you will need to use
the `IsBackgroundTask` property. By default this property is `False`.
This means any derived `AsyncTask` will wait for all `tasks` to complete
before returning to MSBuild.

When the `IsBackgroundTask` property is set to true, `base.Execute` will
not longer block until all `tasks` have completed. Instead it will 
automatically register itself with the `BackgroundTaskManager` and then
return. This feature is especially useful if you want to have code which 
runs in the background while the build continues. 

In this situation what will happen is the background tasks will be started,
and the build will just continue as normal. At the end of the build when 
the `BackgroundTaskManager` is disposed by MSBuild it will wait on all 
the registered `AsyncTask's` before disposing. This means all the background
threads will be completed before the build finishes. 

However there might be situations where you want to start a background task
and then wait at a later point in the build for it to complete. For this we
have the `WaitForBackgroundTasks` Task. This MSBuild Task will wait for all
registered `AsyncTask` items to complete before returning.

The `AsyncTask` has a `Category` property. By default this is `default`. When
`IsBackgroundTask` is set to `True` this `Category` is used to register the 
task with the `BackgroundTaskManager`. `WaitForBackgroundTasks` also has a
`Categories` property. This is a list of the categories it should wait for. 
By default it is `default`. You can use the `Category`/`Categories` to run multiple
different background tasks and wait on them at different parts of the build. 

Here is an example. This makes use of the `MyAsyncTask` we defined earlier, but
supports running it in the background. We then also use the `WaitForBackgroundTasks`
to wait on that task later in the build process.

```xml
  <UsingTask TaskName="Xamarin.Build.WaitForBackgroundTasks" AssemblyFile="$(AsyncTask)">
  <Target Name="_StartLongTask">
    <!-- Start MyAsyncTask in the background -->
    <MyAsyncTask IsBackgroundTask="true" Category="example" />
  </Target>
  <Target Name="WaitForBackgroundTask" DependsOnTarget="_StartLongTask">
      <!-- Wait for it to complete -->
      <WaitForBackgroundTasks Categories="example" />
  </Target>
```

This can give you flexibility to run very long running tasks in the background
without holding up the build process.