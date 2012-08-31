
# Roque

_pronounced "raw-queue"_

Roque is an event & work queueing framework for .Net, made simple.

It sits on top of the C# abstractions you know (plain old C# events and methods), and uses [Redis](http://redis.io/) behind the scenes to make them work in an async, transparent, distributed, scalable, decoupled and failure-proof way.

Message queueing doesn't get simpler than this!

> Really?? ... show me!

## Example 1: Image Processing

Lets say we have a website and we want to build thumbnails for uploaded pics, that's a time-consuming operation we can't perform during the lifetime of web request.

1- Create a service interface.

``` csharp

    public interface IImageProcessor {
        void CreateThumbnail(string filename, int width, int height, AlgorithmOptions options);
    }
``` 

2- Use it on your application:

``` csharp

    public class ImageBiz {
        IImageProcessor ImageProcessor = RoqueProxyGenerator.Create<IImageProcessor>("images");
        public void ImageUploaded(filename){
            ImageProcessor.CreateThumbnail(filename, 160, 120, new AlgorithmOptions { Quality=0.7 });
        }
    }
``` 

Note: add references to Roque.Core and Roque.Redis assemblies to your project.

3- Config a redis-based queue named "images":

``` xml

    <?xml version="1.0"?>
    <configuration>
      <configSections>
        <section name="roque" type="Cinchcast.Roque.Core.Configuration.Roque, Roque.Core"/>
      </configSections>
      <roque>
        <queues>
          <queue name="images" type="Cinchcast.Roque.Redis.RedisQueue, Roque.Redis">
            <settings>
              <setting key="host" value="localhost"/>
              <!-- Optional, if not specified default Redis port is used: 6379 -->
              <setting key="port" value="6379"/> 
            </settings>
          </queue>
        </queues>
      </roque>
    </configuration>
```

That's it. You're already enqueuing jobs!, let's set up a worker, hurry up!:

4- Implement your image processor service:

``` csharp

    public class ImageProcessor : IImageProcessor {
        public void CreateThumbnail(string filename, int width, int height, AlgorithmOptions options = null){
            // a time-consuming task, eg: resize the image and save it adding a suffix
            throw new NotImplementedException();
        }
    }
``` 

5- Install Roque service on a machine (with access to your Redis server).

6- On the same folder of roque.exe drop the assembly(ies) containing IImageProcessor interface and ImageProcessor class.

7- On the worker Roque.exe.config:

``` xml

    <?xml version="1.0"?>
    <configuration>
      <configSections>
        <section name="roque" type="Cinchcast.Roque.Core.Configuration.Roque, Roque.Core"/>
      </configSections>
      <roque>
        <queues>
          <queue name="images" type="Cinchcast.Roque.Redis.RedisQueue, Roque.Redis">
            <settings>
              <setting key="host" value="localhost"/>
            </settings>
          </queue>
        </queues>
        <workers>
          <!-- a worker poping jobs from "images" queue --> 
          <worker name="main" queue="images"/>
        </workers>    
      </roque>
      <castle>
        <components>
          <!-- using Castle Windsor to tell Roque what image processing service to use. type name must be fully qualified --> 
          <component service="Acme.Images.IImageProcessor" type="Acme.Images.ImageProcessor, Acme.Images"/>
        </components>
      </castle>
    </configuration>
```

8- Start Roque Service to start processing images!

(or you can roque.exe from a console, use: roque.exe /debug to attach your VisualStudio and debug your image processor)

You're done, now you can start adding more workers to get automatic load balancing by repeating steps 5 to 8.

To check the status of your queues you can run: roque.exe status 

    C:\>roque status /maxage=10 /maxlength=500000
    Redis send-pump is starting
    roque Information: 0 : [REDIS] connected to localhost:6379
    Queue images has 262570 pending jobs. Next job was created < 1sec ago.
    Queue audiofiles has 3342 pending jobs. Next job was created 12sec ago. [TOO OLD]
    Queue zipping is empty.
    ERROR: 1 queue have too old pending jobs

run roque.exe without arguments to see al options.


> That's awesome! but I want events, I need decoupling, I want multiple and easy to add/replace/remove subscribers. But I don't want to read books on [Message Queues](http://goo.gl/wipxw).

(If you wonder what's the difference check the queue diagrams below showing a work queue and a pub/sub queue)

## Example 2: Website User Sign-up post tasks.

Let's change the approach, let's suppose we want to perform several differnt tasks each time a user signs up. These tasks include creating a thumbnail of users profile pic, and sending a welcome email. (we could add logging, stats, analytics, etc.)

We don't want to clutter our user entity with the execution of this tasks. We already know a good solution to this problem: events.

1- Create an event-throwing interface.

``` csharp

    public interface IUserEvents {
        event EventHandler<UserEventArgs> UserSignedUp;
    }
``` 

2- Throw events on your application

``` csharp

    public class UserBiz : IUserEvents {

        public event EventHandler<UserEventArgs> UserSignedUp;

        public void SignUp(string username, string password, string email) {

            // TODO: insert the user in my database

            var handler = UserSignedUp;
            if (handler != null){
                handler(this, new UserEventArgs(username, email));
            }

            // TIP: if you want you can save a few lines writting an extension method for Exception
            // UserSignedUp.Raise(new UserEventArgs(username, email));
        }
    }

    public class BizEventsInitializer {
        // call this on app startup
        public void Init() {
            // make all events on IUserEvents thrown by this instance available for remote subscription
            RoqueEventBroadcaster.SubscribeToAll<IUserEvents(UserBiz.Instance);
        }
    }
``` 

3- Config redis-based events queue:

``` xml

    <?xml version="1.0"?>
    <configuration>
      <configSections>
        <section name="roque" type="Cinchcast.Roque.Core.Configuration.Roque, Roque.Core"/>
      </configSections>
      <roque>
        <queues>
          <!-- Reserved name _events is used by default by RoqueEventBroadcaster -->
          <queue name="_events" type="Cinchcast.Roque.Redis.RedisQueue, Roque.Redis">
            <settings>
              <setting key="host" value="localhost"/>
            </settings>
          </queue>
        </queues>
      </roque>
    </configuration>
```

Your website is ready!, You're events are available, they'll get in your queues as soon as you add subscribers for them.

Note: If no subscribers are found for an event, nothing is sent to Redis. You _events queue will always be empty (it won't even exist on Redis), as events never get directly enqueued, they get broadcasted to other queues.

4- Add some subscribers:

``` csharp

    public class ThumbnailCreator {
        public void SubscribeTo(IUserEvents userEvents) {
            userEvents.UserSignedUp+= UserEvents_UserSignedUp;
        }
        public void UserEvents_UserSignedUp(object sender, UserEventArgs args) {
            // let's reuse or image processing service here
            new ImageProcessor().CreateThumbnail("pics/"+args.Username+".jpg", 160, 120);
        }
    }
```

``` csharp

    public class UserGreeter {
        public void SubscribeTo(IUserEvents userEvents) {
            userEvents.UserSignedUp+= UserEvents_UserSignedUp;
        }
        public void UserEvents_UserSignedUp(object sender, UserEventArgs args) {
            MailSender.SendWelcomeEmail(args.Username, args.Email);
        }
    }
```

5- Install Roque service on a machine (if you didn't before).

6- On the same folder of roque.exe drop the assembly(ies) containing IUserEvents interface and your ThumbnailCreator and UserGreeter classes.

7- On Roque.exe.config:

``` xml

    <?xml version="1.0"?>
    <configuration>
      <configSections>
        <section name="roque" type="Cinchcast.Roque.Core.Configuration.Roque, Roque.Core"/>
      </configSections>
      <roque>
        <queues>
          <queue name="images" type="Cinchcast.Roque.Redis.RedisQueue, Roque.Redis">
            <settings>
              <setting key="host" value="localhost"/>
            </settings>
          </queue>
          <queue name="greetings" type="Cinchcast.Roque.Redis.RedisQueue, Roque.Redis">
            <settings>
              <setting key="host" value="localhost"/>
            </settings>
          </queue>
        </queues>
        <workers>
          <!-- a worker poping jobs from "images" queue --> 
          <worker name="main" queue="images">
            <subscribers>
                <!-- all events that ThumbnailCreator subscribes to will be broadcasted to this worker's queue (images) --> 
                <subscriber type="Acme.Images.ThumbnailCreator, Acme.Images"/>
            </subscribers>
          </worker>
          <!-- a worker poping jobs from "greettings" queue --> 
          <worker name="main" queue="greetings">
            <subscribers>
                <!-- all events that UserGreeter subscribes to will be broadcasted to this worker's queue (greetings) --> 
                <subscriber type="Acme.Messaging.UserGreeter, Acme.Messaging"/>
            </subscribers>
          </worker>
        </workers>    
      </roque>
    </configuration>
```

Now this requires some explanation. What I'm saying here is, create 2 separate queues, with a worker listening on each queue.

Each worker has a subscriber on it. Roque detects all events a subscriber is attached to (using interceptors on event handlers). That allows Roque to broadcast each event to all queues where there as least one worker, with a subscriber interested on this specific event. 

This means Roque routes event messages automatically for you! Efficiently and without further configuration.

On the publisher side (eg. your website) a lists of subscribed queues is mantained and cached, if a new type of subscriber is found in any worker cache clear request is sent with a Redis PUB/SUB message.

This means you can just drop a new subscriber at any worker and your website(s) will immediately start sending the events you expect (and nothing more!).

8- Start (or restart) Roque Service.

Now you can check the status of your queues and you should see the "user signed up" event being copied to both queues:

'''

    C:\>roque status
    Redis send-pump is starting
    roque Information: 0 : [REDIS] connected to localhost:6379
    Queue images has 14 pending jobs. Next job was created 4sec ago.
    Queue grettings has 434 pending jobs. Next job was created 1min 12sec ago.
'''

Note: This example seems to show that my mail sender is not keeping the pace, I might have to add more workers on the greetings queue, or check the speed of my SMTP server.

You can check event subscriptions by running roque.exe events

'''

    C:\>roque events
    Redis send-pump is starting
    roque Information: 0 : [REDIS] connected to localhost:6379
    Queue _events has 1 event with subscribers
       Acme.MySite.Biz.IUserEvents:UserSignedUp is observed by images, grettings
'''

## Benchmarks

On a very preliminar and simple benchmark with this conditions:

- 100K messages (jobs/tasks/events).
- Running Redis, publisher and workers on a single machine.
- An almost zero-effort job (we just want to test the engine)

We got:

- Enqueueing ~ 47.6K jobs per second
- Dequeueing with 1 worker ~ 4.6K jobs per second
- Dequeueing with 3 workers ~ 9.9K jobs per second 

Note: Dequeuing speed can be increased by adding more workers (and eventually more Redis clusters).Although running multiple workers on same machine doesn't make a lot of sense, a more significant improvement would come from running workers on different machines.

## Features

- Queues are persisted on Redis. Redis is *fast*, scalable and simple to set up. (Others storages can be plugged in)
- Transparent integration. Just call your methods, raise your events. They already make your intent clear, no need for complex message routing configurations, *DRY*.
- You keep your code strong-typed (compile-time checks, intellisense, refactoring) and completely agnostic of the queueing mechanism. 
- Jobs are stored as simple JSON objects that any person or app can read.
- Scalability. If your queues are getting full, just start more workers. You can have multiple distributed worker instances picking jobs from the same queue, work load gets balanced. Workers can be added or removed at any time. Multiple publishers are supported too.
- On workers, service classes are resolved using IoC, so you can easy swap implementations.
- Run on console (useful for debugging) or as a windows service.
- Built-in support for resuming jobs. If a worker is shut down unexpectedly, when restarted it will retry the same job.
- Configure retrying rules (time to wait before retrying, max number of times) based on Exception types.
- Minimal latency. By using Redis no polling is done, jobs are pushed immediately to the first available worker. pushing and popping is *fast* (Redis LPUSH / BRPOPLPUSH based).
- Monitor queue status, and check when queues are getting too long (need more workers?), or jobs are getting too old (workers are down or disconnected?).
- Supports 2 message queue patterns:
  - Work queues (by invoking methods). eg. request the execution of a job asynchronously.
  - Message broadcasting (pub/sub) in front of work queues (by raising events). eg. notify multiple subscriptors that perform jobs on specific events.

## Queue Patterns

Roque supports 2 type of queues:

### Work Queue

<img src="https://raw.github.com/benjamine/Roque/master/mq_work.png" alt="work queue"/>

This type of queue is used when you directly invoke a method in a proxy (check Example 1)

- (P)roducer here is a dynamic proxy built using: RoqueProxyGenerate.Create<IMyService>("queuename");
- A message is sent to the queue on each method invocation
- queues are redis lists
- (C)consumer are Roque Workers that instantiate a service class (implementing IMyService), can be run on console or a window service instance.

### Pub/Sub (in front of Work Queues)

<img src="https://raw.github.com/benjamine/Roque/master/mq_pubsub_work.png" alt="pub/sub queue"/>

Here a new actor appears to introduce decoupling between producer and consumers, this what we want when we create events in C#.
So this type of queue is used when raise events that are observed by a RoqueEventBroadcaster. (check Example 2)

- (P)roducer here is a RoqueEventBroadcaster object that's subscribed to specific events in your app: new RoqueEventBroadCaster().HandleEvents<MyInterfaceWithEvents>(objectImplementingMyInterfaceWithEvents);
- A message is sent to the queue each time the event raises (only if there are subscribers listening)
- (B)roadcaster, as you may guess, your RoqueEventBroadCaster object. He copies the message to each queue where there's a subscriber waiting. On Redis a SortedSet of subscribed queues is maintained for each specific C# event you subscribe to. Subscriber sets are cached by broadcaster and on any change they get notified in realtime using a Redis PUB/SUB message. 
- Once the message is copied into each queue all continues as in a Work Queue (each subscribed queue is a Work Queue)

## Retrying

If a job fails (either in a work service class or an event handler provided by a subscriber class) Roque allows you to specify if the Worker should retry to execute it.

### RetryOnAttribute

``` csharp

    // (on all class methods) if the thumbnails file server is down, keep retrying until someone fix it
    [RetryOn(typeof(ThumbnailsFileServerNotFoundExceptin), DelaySeconds=30, MaxTimes=100)]
    public class ImageProcessor : IImageProcessor {
        public void CreateThumbnail(string filename, int width, int height, AlgorithmOptions options = null){
            // a time-consuming task, eg: resize the image and save it adding a suffix
            throw new NotImplementedException();
        }
    }
``` 

### DontRetryOnAttribute

``` csharp

    // always retry any method if unexpected exceptions occur
    [RetryOn(typeof(ThumbnailsFileServerNotFoundExceptin), DelaySeconds=30)]
    public class ImageProcessor : IImageProcessor {

        // if the original file is not found, the user must have deleted the image, don't retry
        [DontRetryOn(typeof(FileNotFoundExceptin))]
        public void CreateThumbnail(string filename, int width, int height, AlgorithmOptions options = null){
            // a time-consuming task, eg: resize the image and save it adding a suffix
            throw new NotImplementedException();
        }
    }
```

Please note:

- These attribute can be specified at method or class level
- Multiple attributes can be applied (exception types are compared with the "is" operator from top to bottom like in catch {} blocks)
- by default jobs are never retried

## Requirements

- Microsoft .Net Framework 4.0
- Redis

## License

(The MIT License)

Copyright (c) 2012 Cinchcast <contact@cinchcast.com>

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the 'Software'), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

