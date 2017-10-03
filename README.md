# Xamarin.Build.AsyncTask

Provides the AsyncTask to streamline the creation of long-running tasks that 
are cancellable and don't block the UI thread in Visual Studio.

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
  <UsingTask TaskName="MyAsyncTask" TaskFactory="CodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
    <ParameterGroup>
      <!-- TODO: your task parameters -->
    </ParameterGroup>
    <Task>
      <Reference Include="$(AsyncTask)" />
      <Reference Include="System.Threading.Tasks"/>
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