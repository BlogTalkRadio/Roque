# Roque Worker

This project contains a Roque worker (services and/or event subscribers), this will run in a windows service (or console) listening to events or method calls (accross the wire).

## Config files

Configuration is on Roque.exe.config.

- the _events queue and the queue this subscriber will be attached to
- a worker containing this project subscriber
- logging settings
- other settings specific to this subscriber

Additional config transformations can be used for build targets (eg. Roque.exe.debug.config), using these Roque.exe.config is generated on compilation.

Note: when installing or updating Roque nuget package on this project it will create an app.config file, that should be deleted as roque.exe.config is copied to output folder instead.

## Adding Subscribers

Subscriber classes are classes that subscribe to events in an interface referenced from an assembly shared between workers and emitter application.
To create one follow these steps:

### 1. Create class

Add a new class to the project, and add a method starting with "Subscribe".

``` csharp

    public partial class ExampleSubscriber
	{
		public void SubscribeToUser(IUserEvents userEvents)
		{
		}
	}
```

This class must have a parameter-less constructor. 

All methods in this class starting with "Subscribe" will be called when starting a worker (console or windows service).
Parameters will be dynamic proxies to simulate the original emitter object, no other parameters are allowed on this methods.

At this point you'll need to add a reference to shared assembly/project containing event interfaces.

Although you can add multiple parameters to this methods, for clarity is recommended to use 1 method for each interface you want to subscribe to.

### 2. Subscribe to events

In this methods you subscribe to events you are interested on:

``` csharp

	public void SubscribeToUser(IUserEvents userEvents)
	{
		userEvents.Registered += UserEvents_Registered;
		userEvents.Confirmed += UserEvents_Confirmed;
		userEvents.LoggedIn += UserEvents_LoggedIn;
	}
```

Roque will detect this subscriptions and report to all interested broadcasters which events should be routed to the Queue this worker is attached to.
This is done immediately by adding the queue to the subscriptors set in Redis, and by sending a pub/sub message informing of the change.

### 3. Configure retrying rules

This is an optional step.

By default if an event handler throws an Exception, Roque will log the error and continue with next job.
But you can tell Roque to retry specific jobs based on Exception types.

If you decide to do so, at this point you need to add Roque nuget package to this project. Then you can:

``` csharp

    [DontRetryOn(typeof(FatalException))]
    [RetryOn(typeof(Exception), DelaySeconds = 15)]
    public partial class ExampleSubscriber
    {
		[RetryOn(typeof(HttpException), DelaySeconds = 15, MaxTimes = 5)]
		public void UserEvents_Registered(object sender, UserEventArgs args)
		{
		}
    }
```

RetryOn and DontRetryOn attributes can be applied at method or class level. They are evaluated in a similar way to catch {} blocks, in this order:

- Top to bottom, at method
- Top to bottom, at class

On first match (where Exception type is equal or a subclass of thrown Exception) the rule is applied. A delay to wait before retrying can be specified and an an optional maximum number of retries.

Alternatively you can throw a ShouldRetryException in your code to get similar results.

### 4. Declare on config

In order to Roque instantiate your subscriber you must declare it (using fully qualified name) on configuration

``` xml

    <roque>
        <queues>
            <queue name="example" type="Cinchcast.Roque.Redis.RedisQueue, Roque.Redis">
                <settings>
                    <!-- for RedisQueue available settings are host and port -->
                    <setting key="host" value="localhost" />
                </settings>
            </queue>
            <queue name="_events" type="Cinchcast.Roque.Redis.RedisQueue, Roque.Redis">
                <settings>
                    <setting key="host" value="localhost" />
                </settings>
            </queue>
        </queues>
        <workers>
            <!-- define your workers, typically you'll have one worker named like the queue he's attached to -->
            <!-- autoStart="true" tells Roque to run this worker on windows service or when started with: roque.exe work -->
            <worker name="example" queue="example" autoStart="true">
                <!-- classes that subscribe to events -->
                <subscribers>
                    <!-- subscriber type, fully qualified name is required -->
                    <subscriber type="Acme.ExampleSubscriber, ExampleSubscriber"/>
                </subscribers>
            </worker>
        </workers>
    </roque>
```

## Adding Services

Service classes are implementations for services directly invoked on the application.
To create one follow these steps:

### 1. Create class

Add a new class to the project, and implement the service interface(s).

``` csharp

    public partial class ExampleService : IExampleService
	{
        // IExampleService methods
	}
```

This class must have a parameter-less constructor. 

At this point you'll need to add a reference to shared assembly/project containing the service interface.

### 2. Configure retrying rules

This is an optional step.

By default if an event handler throws an Exception, Roque will log the error and continue with next job.
But you can tell Roque to retry specific jobs based on Exception types.

If you decide to do so, at this point you need to add Roque nuget package to this project. Then you can:

``` csharp

    [DontRetryOn(typeof(FatalException))]
    [RetryOn(typeof(Exception), DelaySeconds = 15)]
    public partial class ExampleService : IExampleService
    {
		[RetryOn(typeof(HttpException), DelaySeconds = 15, MaxTimes = 5)]
		public void DoSomething(string arg1, int arg2)
		{
		}
    }
```

RetryOn and DontRetryOn attributes can be applied at method or class level. They are evaluated in a similar way to catch {} blocks, in this order:

- Top to bottom, at method
- Top to bottom, at class

On first match (where Exception type is equal or a subclass of thrown Exception) the rule is applied. A delay to wait before retrying can be specified and an an optional maximum number of retries.

Alternatively you can throw a ShouldRetryException in your code to get similar results.

### 3. Declare on config

In order to Roque instantiate your service class you must declare it (using fully qualified name) on configuration.
Services are obtained using Castle Windsor.

``` xml

    <castle>
        <!-- implementations for work queue services -->
        <components>
            <component service="Acme.IExampleService" type="Acme.ExampleService, ExampleService"/>
        </components>
    <castle>
```

## Running on console

On development you can run roque in console mode. That way you can debug step-by-step or test your subscriber looking at verbose output.

When install Roque.Worker package you'll get powershell command that you can run from the VisualStudio Package Manager Console.

IMPORTANT: Before running commands on Package Manager Console check the selected default project.

Now you can type ```Roque-``` and press TAB to see the list:

    Roque-Work // start "roque work" in a new console windows
    Roque-Work-Debug // start "roque work /debug" in a new console windows
    Roque-Status // show queues status
    Roque-Events // show event subscriptions
    Roque-Run <params> // run any roque <params> command on the Package Manager console

These commands will always copy (and update only when needed) roque binaries to output dir (they use ```roque copybinaries``` command). 

Roque supports hot-deploy, so if you have roque running you can just build the project and roque will reload automatically:

    roque Information: 0 : Worker example waiting job on example
    roque Information: 0 : [FileWatcher] file change detected: Roque.exe.config (Changed)
    roque Information: 0 : Stopping...
    roque Information: 0 : Worker stopped: example
    roque Information: 0 : Stopped
    roque Information: 0 : Starting...
    roque Information: 0 : Worker example started. AppDomain: RoqueWorkers9e8ebbbf-3e06-455e-a3a1-e53f0efc7387

Note: If you have added Roque Nuget Package to this project building with roque running will fail when trying to overwrite Roque dlls, Roque.Worker package sets Copy Local = false for this references on installation to avoid that error.

## Deploying as Windows Service

### Installing a new instance

1. On the server you want a roque instance to run with this worker, create a folder, eg:

    c:\Services\RoqueWorkers\ExampleSubscriber

2. Add roque binaries (on nuget package tools folder) to this folder. Run ```roque copybinaries``` command on target folder.

3. Copy this project output to the target folder, created on step 1.

4. Check roque.exe.config for this environment (typically you'll want to check trace settings and redis host)

4.  Install as windows service using [SC](http://support.microsoft.com/kb/251192), binPath must be absolute path to roque.exe, eg:

    sc create ExampleSubscriber binPath=c:\Services\RoqueWorkers\ExampleSubscriber\roque.exe

5. Use SC or Services administration tool to finish service configuration (account, start mode, etc.)

### Updating instances

Roque supports hot-deploy so for updating you can just repeat step 2 described above, and service will reload automatically.