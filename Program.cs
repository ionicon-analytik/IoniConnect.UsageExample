////////////////////////////////////////////////////////////////
//                                                            //
// IoniConnect.UsageExample                                   //
// ------------------------                                   //
//                                                            //
// Showcase, demonstration and documentation by example       //
// of the Ionicon AME HTTP-API.                               //
//                                                            //
// You should have received a copy of the IoniConnect.nupgk   //
// NuGet-package together with this source code.              //
// This requires the IoniConnect.API to run on port 5066.     //
// The API version is displayed in the example /api/status    //
// below and should conform to the NuGet package version.     //
//                                                            //
// Author:                                                    //
//  moritz.koenemann@ionicon.com                              //
//  software@ionicon.com                                      //
//                                                            //
// Version history:                                           //
//                                                            //
// v5 -  9.Dez 2025                                           //
//  * changed: api.GetFile() / .SendFile() takes 'query'      //
//    as *last* argument, consistent with all other API calls //
//  * update IoniConnect.nupgk v1.0.9                         //
//                                                            //
// v4 -  7.Oct 2025                                           //
//  * changed: use a PATCH request to stop a measurement      //
//                                                            //
// v3 - 28.Aug 2025                                           //
//  * add example 'download the result files and report'      //
//  * update IoniConnect.nupgk v1.0.7                         //
//                                                            //
////////////////////////////////////////////////////////////////
using IoniConnect;
using IoniConnect.Models;

Console.WriteLine(@"
=========== checking the connection/status ============

connect the API and print some infos...
");

// first, let's use this helper class to start making our HTTP-requests.
// Note: there is also an `async` variant for this (`APIConnectorAsync`)
//  that provides the same methods as described here, but with an -Async suffix
var api = new IoniConnect.Http.APIConnector("http://localhost:5066");

// just a placeholders for later use...
HttpResponseMessage r;
Newtonsoft.Json.Linq.JObject jObject;
string href = "";

// check an endpoint that's always available to see if we're connected:
var status = api.GetJson("/api/status");
// Note: all methods of the `APIConnector` swallow most Exceptions that occur
//  on connection problems and instead return an empty object as default:
if (!status.HasValues)
{
    Console.WriteLine("error: no connection to API");
    Thread.Sleep(3 * 1000);
    return;
}
Console.WriteLine(new
{
    SerialNr = status["instrumentSerialNr"].ToObject<string>(),
    Version = status["version"].ToObject<string>(),
});


/////////////////////////////////////////////////////////

Console.WriteLine(@"
=========== getting a list of actions ============

this is an example of getting any kind of collection
within the API and here we list the (predefined) actions.
");

// let's have a look at a collection of elements...
jObject = api.GetJson("/api/actions");
var count = jObject["count"].ToObject<int>();
Console.WriteLine($"we are counting ({count}) actions");
if (count > 0)
{
    // ...that is always organized underneath an `_embedded` object:
    //  this contains in most cases an object of the same name as
    //  requested in the endpoint, which is a JSON array of objects.
    var jArray = jObject["_embedded"]["actions"];
    var element = jArray[0];

    var action = element.ToObject<IoniConnect.Models.Action>();
    href = element["_links"]["self"]["href"].ToObject<string>();

    Console.WriteLine($"found '{action.Name}' with endpoint '{href}'...");
}

// One common data-structure is this kind of collection within an "_embedded" element.
// We provide an adapter that implements an `ICollection` to simplify the C-R-U-D protocol.
// Let's use the same endpoint as above, but with our collection-helper.
// The type of the `generic` must of course match the type provided by the endpoint.
// All neccessary types can be found in the `IoniConnect.Models` namespace:
var actions = new IoniConnect.ReadOnlyEmbeddedCollection<IoniConnect.Models.Action>(api, "/api/actions");

// now, we can iterate over the elements
foreach (var action in actions)
{
    Console.WriteLine(new
    {
        action.Name,
        action.Duration_Runs,
        action.AME_ActionNumber,
    });
}

#region +++ [advanced] modifying lists using an EmbeddedCollection +++
// not for `IoniConnect.ReadOnlyEmbeddedCollection` !!
/*

// add some elements:
if (actions.Count == 0)
{
    actions.Add(new IoniConnect.Models.Action
    {
        Name = "Last Action Hero",
        AME_ActionNumber = 1,
        Duration_Runs = 17,
    });
    actions.Add(new IoniConnect.Models.Action
    {
        Name = "The Red Tide",
        PostActionNumber = 1,
    });
}

// to modify an element, we can use a common pattern...
// EXTRACT
var firstAction = actions[0];
// TRANSFORM
firstAction.Name = "Django Unchained";
// LOAD (note, that only the item-setter emulates a PUT-request)
actions[0] = firstAction;

// although this is implemented for the `ICollection` interface, it is not allowed to delete actions:
//actions.RemoveAt(0);  // DON't ~> will throw with `405: method not allowed`

*/
#endregion


/////////////////////////////////////////////////////////

Console.WriteLine(@"
=========== setting up a server-sent-event listener ============

This lets us follow the events of the AME-system during the measurement.
Especially useful for fetching the current (component-)data during the 
run, which is exemplified below.
");
// no need to worry about thread-safety in this simple example,
// but one would normally use something like a ConcurrentQueue here:
var events = new List<string>();
var componentData = new List<IReadOnlyCollection<IoniConnect.Json.Models.Quantity>>();

void Sse_MessageHandler(object? sender, string href)
{
    if (sender is IoniConnect.ServerSentEventListener sse)
    {
        // collect all occurring events in this list:
        events.Add(sse.Topic);

        // we can always follow the link passed as event-data:
        var j = api.GetJson(href);

        if (sse.Topic == IoniConnect.TopicString.POST_average_components)
        {
            // for this special event, we can download the current trace-data
            // ("component" data), which resolves again to a well-known collection:
            var embedded = new IoniConnect.ReadOnlyEmbeddedCollection<IoniConnect.Json.Models.Quantity>(api, href, collectionName: "quantities");

            componentData.Add(embedded.ToList());
        }
    }
}

// get a token, otherwise it would truely 'ListenForever()'...
var cts = new CancellationTokenSource();

// ...and spawn a thread, because our listener blocks (an async-method *is* 
// available, but I would not trust my own implementation of that!)
var t = new Thread(() =>
{
    var sse = new IoniConnect.ServerSentEventListener(new UriBuilder(api.Uri) { Path = "/api/events" }.Uri);
    sse.MessageHandler += Sse_MessageHandler;
    sse.ListenForever(cts.Token);
});
t.Start();


/////////////////////////////////////////////////////////

Console.WriteLine(@"
=========== how to START a measurement ============

we POST a new element in the list of measurements, which points to 
a 'RecipeDirectory' containing the configuration. They are exclusively
found in 'C:/Ionicon/AME/Recipes/' and must contain a 'Composition[.json]'
file and a '*.ionipt' peaktable.

Let's ask the API for a list of valid paths:
");
var recipes = new ReadOnlyEmbeddedCollection<IndexedFile>(api, "/api/recipes")
    .Select(file => file.Path)
    .ToList();

foreach (var choice in recipes.Select((path, i) => $"[{i}]: " + path))
{
    Console.WriteLine(choice);
}
Console.WriteLine("\nselect a recipe by entering a number: ");
int ix = int.Parse(Console.ReadKey(false).KeyChar.ToString());
Console.WriteLine();
string recipe = recipes[ix];

// first, check what's going on...
jObject = api.GetJson("/api/measurements/current");

// this call does not communicate errors and instead just gives us
// a default object! Testing for '.HasValues' is a good way to check this:
bool error = !jObject.HasValues;
if (error)
{
    // so, the `GET /api/measurements/current` is [410: Gone]...
    Console.WriteLine("no measurement is currently running");

    // ...and we can create a new measurement to start:
    r = api.SendJson(HttpMethod.Post, "/api/measurements", new
    {
        RecipeDirectory = recipe,
    });
    Console.WriteLine($"`POST /api/measurements` returned [{r.StatusCode}]");

    // the API will check if the 'recipeDirectory' is valid:
    if (!r.IsSuccessStatusCode)
    {
        Console.WriteLine("\nthis didn't work... are you sure the recipe-directory exists?");
    }
}
else  // `GET /api/measurements/current` is [200: OK]...
{
    // ...what do we have running??
    var meas = jObject.ToObject<IoniConnect.Models.Measurement>();

    Console.WriteLine($"currently running recipe '{meas.RecipeDirectory}'");
}


/////////////////////////////////////////////////////////

Console.WriteLine(@"
=========== how to schedule an action ============

we are sending a LINK request to `/api/actions/pending` which links
this placeholder with the hyper-reference to the desired action.

(that sounds more complicated than it is...)

Note, that this won't work as intended on the DEMO configuration!!

let's wait for 30 seconds for the measurement to continue...
");
Thread.Sleep(30 * 1000);

// Note: this is not strictly neccessary, because it would be
//  done by the AME-system, but if the "/pending" slot is occupied,
//  the following request will fail with a [409: Conflict]:
r = api.Request(HttpMethod.Delete, "/api/actions/pending", "");

// the endpoint "/api/actions/1" must of course exist and point
// to the action we want to trigger (see examples above):
r = api.LinkLocation("/api/actions/pending", "/api/actions/1");

Console.WriteLine($"`LINK /api/actions/pending` returned [{r.StatusCode}]");


/////////////////////////////////////////////////////////

Console.WriteLine(@"
=========== how to STOP a measurement ============

we GET the current measurement and PUT the 'isRunning' property to false.

let's wait for 30 seconds for the measurement to continue...
");
Thread.Sleep(30 * 1000);

// again, check what's going on...
jObject = api.GetJson("/api/measurements/current");

if (jObject.HasValues)
{
    // we need the link and luckily the hyper-reference to "self",
    // meaning the resolved endpoint to our request, is ALWAYS provided:
    href = jObject["_links"]["self"]["href"].ToObject<string>();

    r = api.SendJson(new HttpMethod("PATCH"), href, new { IsRunning = false });

    Console.WriteLine($"`PUT {href}` returned [{r.StatusCode}]");
}

/////////////////////////////////////////////////////////

Console.WriteLine(@"
=========== download the result files and report ============

The results of the measurement are saved in directories usually
such as D:\AMEData\<current datetime>, where each new-folder-action
would create a new directory. These are attached as the /results-
endpoint for a given /api/measurement/:id that we just obtained.

We will now navigate to the /api/measurement/X/results/last, 
download all files as a ZIP-archive and get a report for the
top 5 TVOCs in XML format...
");
if (string.IsNullOrEmpty(href))  // should have been set to /api/measurement/<current> above...
{
    jObject = api.GetJson("/api/measurements/last");

    href = jObject["_links"]["self"]["href"].ToObject<string>();
}
var results = new ReadOnlyEmbeddedCollection<IndexedFile>(api, href + "/results")
    .Select(file => file.Name)
    .ToList();

jObject = api.GetJson(href + "/results/last");  // href should be "/api/measurements/X"

if (jObject.HasValues)
{
    href = jObject["_links"]["self"]["href"].ToObject<string>();  // href should be "/api/measurements/X/results/Y"

    api.GetFile(href + "/download", "./result.zip", query: "exclude=*.h5");  // use "exclude=*.h5&exclude=*.tsv" for more exclusions

    Console.WriteLine("Result files saved as " + Directory.GetCurrentDirectory() + "\\result.zip");

    var xml = api.GetContent(href + "/report");

    Console.Write(xml.Substring(0, 200));
    Console.WriteLine("...");
}

/////////////////////////////////////////////////////////

Console.WriteLine(@"
=========== observing the AME events ============

let's have a look at the events dispatched along this litte program...
");

foreach (var e in events)
{
    Console.WriteLine(e);
}

if (componentData.Count == 0)
{
    Console.WriteLine("\nno components have been collected :/");
}
else
{
    Console.WriteLine($"\nwe collected the component-data for ({componentData.Count}) averages, nice!");

    var data = componentData.Last();
    foreach (var x in data)
    {
        jObject = api.GetJson("/api/components/" + x.Id.ToString());

        Console.WriteLine(new
        {
            Name = jObject["shortName"].ToObject<string>(),
            x.Id,
            x.Value,
            x.Error,
        });
    }
}

/////////////////////////////////////////////////////////

Console.WriteLine("\nDone. Press the 'any' key to quit!");
if (new ConsoleKeyInfo[] { Console.ReadKey(true) }.Any())
{
    Console.WriteLine("\n...but there is no 'any' key ?!?!?");
    // we don't need this anymore and it prevents the program from closing:
    cts.Cancel();
    t.Join();

    return;
}
