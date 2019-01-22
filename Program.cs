using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using static System.Console;
using static System.Convert;

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.ServiceBus;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureFunctionConsumer
{
    class Program
    {
        private static CloudBlobClient blobClient;
        private static CloudQueue storageAccountQueue;
        private static EventHubClient eventHubClient;
        private static HttpClient httpClient;
        private static IQueueClient queueClient;
        private static DocumentClient documentClient;
        static Program()
        {
            httpClient = new HttpClient();
        }
        static void Main(string[] args)
        {
            bool keepGoing = true;
            try
            {
                do
                {
                    var selection = DisplayMenu();
                    switch (selection)
                    {
                        case 1:
                            WriteLine("You selected Event Hub.");
                            MainEventHubAsync(args).GetAwaiter().GetResult();
                            break;
                        case 2:
                            WriteLine("You selected Storage Queue.");
                            MainStorageQueueAsync(args).GetAwaiter().GetResult();
                            break;
                        case 3:
                            WriteLine("You selected Blob Storage.");
                            MainBlobStorageAsync(args).GetAwaiter().GetResult();
                            break;
                        case 4:
                            WriteLine("You selected Service Bus.");
                            MainServiceBusAsync(args).GetAwaiter().GetResult();
                            break;
                        case 5:
                            WriteLine("You selected Cosomos DB.");
                            MainCosmosDBAsync(args).GetAwaiter().GetResult();
                            break;
                        case 6:
                            WriteLine("You selected HTTP Trigger.");
                            MainHTTPTriggerAsync(args).GetAwaiter().GetResult();
                            break;
                        case 7:
                            WriteLine("You selected Event Grid.");
                            WriteLine("NOT YET IMPLEMENTED.");
                            break;
                        case 8:
                            WriteLine("You selected Table Storage.");
                            WriteLine("NOT YET IMPLEMENTED.");
                            break;
                        case 9:
                            WriteLine("You selected Microsoft Graph.");
                            WriteLine("You need to run this one from a browser and send your AAD credentials.");
                            break;
                        case 10:
                            WriteLine("You selected SendGrid.");
                            WriteLine("NOT YET IMPLEMENTED.");
                            break;
                        case 11:
                            WriteLine("You selected SignalR.");
                            WriteLine("NOT YET IMPLEMENTED.");
                            break;
                        case 12:
                            WriteLine("You selected Timer.");
                            WriteLine("This one is run completely from the portal.");
                            break;
                        case 13:
                            WriteLine("Bye.");
                            keepGoing = false;
                            break;
                        default:
                            throw new InvalidOperationException("You entered an invalid option.  Bye.");
                    }
                } while (keepGoing);
            }
            catch (Exception ex)
            {
                WriteLine($"Well...something happend that wasn't expected, specifically: {ex.Message}");
            }
        }
        static public int DisplayMenu()
        {
            WriteLine();
            WriteLine("1.  Event Hub");
            WriteLine("2.  Storage Queue");
            WriteLine("3.  Blob Storage");
            WriteLine("4.  Service Bus");
            WriteLine("5.  Cosomos DB");
            WriteLine("6.  HTTP Trigger");
            WriteLine("7.  Event Grid");
            WriteLine("8.  Table Storage");
            WriteLine("9.  Microsoft Graph");
            WriteLine("10. SendGrid");
            WriteLine("11. SignalR");
            WriteLine("12. SignalR");
            WriteLine("13. Exit");
            WriteLine("Which would you like to trigger?  Enter '13' to exit.");
            var result = ReadLine();
            return ToInt32(result);
        }
        #region Blob Storage
        //Use the Event Grid trigger instead of the Blob storage trigger for blob-only storage accounts
        private static async Task MainBlobStorageAsync(string[] args)
        {
            WriteLine("Enter your Blob Storage connection string:");
            var BlobStorageConnectionString = ReadLine();
            while (BlobStorageConnectionString.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Blob Storage connection string:");
                BlobStorageConnectionString = ReadLine();
            }
            WriteLine("Enter the Blob Container name:");
            var BlobContainerName = ReadLine();
            while (BlobContainerName.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter the Blob Container name: (lowercase only letters!)");
                BlobContainerName = ReadLine().ToLower();
            }
            WriteLine("Enter number of blobs to add: ");
            int BlobsToSend = 0;
            while (!int.TryParse(ReadLine(), out BlobsToSend))
            {
                WriteLine("Try again, this value must be numeric.");
            }
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(BlobStorageConnectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
            WriteLine("Creating blob container...");
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(BlobContainerName);
            if (await blobContainer.CreateIfNotExistsAsync())
            {
                WriteLine($"Blob container '{blobContainer.Name}' Created.");
            }
            else
            {
                WriteLine($"Blob container '{blobContainer.Name}' Exists.");
            }
            await SendBlobsToBlobStorage(BlobsToSend, blobContainer);
        }
        private static async Task SendBlobsToBlobStorage(int numBlobsToSend, CloudBlobContainer container)
        {
            for (var i = 0; i < numBlobsToSend; i++)
            {
                try
                {
                    var blob = $"Blob {i}";
                    WriteLine($"Sending blob: {blob} named helloworld{i}.txt to {container.Name}");
                    CloudBlockBlob blockBlob = container.GetBlockBlobReference($"helloworld{i}.txt");
                    blockBlob.UploadTextAsync($"Hello, World! {i}").Wait();
                }
                catch (StorageException se)
                {
                    WriteLine($"StorageException: {se.Message}");
                }
                catch (Exception ex)
                {
                    WriteLine($"{DateTime.Now} > Exception: {ex.Message}");
                }

                await Task.Delay(10);
            }

            WriteLine($"{numBlobsToSend} blobs sent.");
        }
        #endregion
        #region Event Hubs
        private static async Task MainEventHubAsync(string[] args)
        {
            WriteLine("Enter your Event Hub connection string:");
            var EventHubConnectionString = ReadLine();
            while (EventHubConnectionString.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Event Hub connection string:");
                EventHubConnectionString = ReadLine();
            }
            WriteLine("Enter your Event Hub name:");
            var EventHubName = ReadLine();
            while (EventHubName.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Event Hub name:");
                EventHubName = ReadLine();
            }

            WriteLine("Enter your path where the Video Event files are:");
            var pathofVidEVTfiles = ReadLine();
            while (pathofVidEVTfiles.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your path where the Video Event files are:");
                pathofVidEVTfiles = ReadLine();
            }

            WriteLine("Enter number of events to add: ");
            int EventHubEventsToSend = 0;
            while (!int.TryParse(ReadLine(), out EventHubEventsToSend))
            {
                WriteLine("Try again, this value must be numeric.");
            }

            var connectionStringBuilder = new EventHubsConnectionStringBuilder(EventHubConnectionString)
            {
                EntityPath = EventHubName
            };
            eventHubClient = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());
            await SendMessagesToEventHub(EventHubEventsToSend, pathofVidEVTfiles);
            await eventHubClient.CloseAsync();
        }
        private static async Task SendMessagesToEventHub(int numMessagesToSend, string pathofVidEVTfiles)
        {
            var pathofFiles = System.IO.Directory.GetFiles(pathofVidEVTfiles);
            //eventHubClient.CreateBatch(BatchOptions options)
            for (var i = 0; i < numMessagesToSend; i++)
            {
                if (i < pathofFiles.Length)
                {
                    try
                    {
                        string eventpayload = System.IO.File.ReadAllText(pathofFiles[i]);
                        // string eventpayload = 
                        //     "{\"humanDetection\": {    \"top\": {      \"fine\": {        \"score\": 0.432296664      },      \"coarse\": {        \"score\": 0.803005338,        \"confident\": true      }    },    \"vista\": {      \"fine\": {        \"score\": 0.997097731,        \"confident\": true      },      \"coarse\": {        \"score\": 0.997097731,        \"confident\": true      }    },    \"boundingBox\": {      \"tl\": {        \"x\": 402.2199,        \"y\": 225      },      \"br\": {        \"x\": 432.481628,        \"y\": 326.9399      }    },    \"bottom\": {      \"fine\": {        \"score\": 0.5632303      },      \"coarse\": {        \"score\": 0.993036866,        \"confident\": true      }    },    \"gender\": {      \"fine\": {        \"score\": 0.7193463,        \"confident\": true      },      \"coarse\": {        \"score\": 0.7501131      }    },    \"detectionScore\": 0.584130466,    \"hat\": {      \"fine\": {        \"score\": 0.225411609,        \"label\": \"EDGE\"      },      \"coarse\": {        \"score\": 0.6325879,        \"label\": \"HAS_HAT\"      }    }  },  \"sequence\": 1824,  \"imagePath\": \"production\\CAMAS_TEMPLE2\\2018\\8\\17\\13\\56\\14\\1824.jpg\",  \"hangingWeightDetection\": {    \"boundingBox\": {      \"tl\": {        \"x\": 432.953217,        \"y\": 19.6039238      },      \"br\": {        \"x\": 490.144745,        \"y\": 97.0612946      }    },    \"label\": \"Crane\",    \"score\": 0.9653345  },  \"sourceName\": \"CAMAS_TEMPLE2\",  \"boundingBox\": {    \"tl\": {      \"x\": 402.2199,      \"y\": 225    },    \"br\": {      \"x\": 432.481628,      \"y\": 326.9399    }  },  \"coreVersion\": \"1.17.16\",  \"timestamp\": \"2018-08-17T13:56:14.622Z\",  \"source\": \"rtsp://live:ternium2010@10.190.37.106/Streaming/Channels/101?transportmode=unicast\",  \"humanParsingResult\": {    \"ppeTorso\": {      \"noVest\": 0.0799496,      \"reflectiveStripes\": 0.875546932,      \"hasVest\": 0.0445035361    },    \"nearnessMap\": [      0.8393264,      -0.06802458,      -0.9798625,      -2.4396863,      -1.62732148,      -2.19386172,      -1.32342529,      -1.8112,      0.2637029,      -1.66759849,      -3.1992538,      -2.22771,      0.0680223256,      -0.722229362,      -1.87300289,      -0.39413473,      -1.22235525,      -11.4249353,      -1.0647198,      -1.605865,      -2.14461,      -1.20437384,      -1.56389284,      0.5187794,      -6.18620062,      -3.72475076,      -3.41499138,      -0.633272648,      -0.633885,      -1.89968252,      -4.929584,      -14.662818,      -10.1892014,      -3.72639751,      1.46672308,      1.65621114,      -5.350315,      -5.423319,      -11.4875565,      -12.3585739,      -11.216279,      1.40914929,      -0.8524672,      -4.728786,      -5.156611,      -12.20564,      -11.0378141,      -6.57869959,      3.23019361,      1.394465,      1.06809986,      2.172632,      4.470751,      3.49655247,      1.66838634,      2.93546915,      2.61168647,      2.139344,      3.712688,      4.318213,      4.54218149,      2.81327486,      2.42701912,      0.5283245,      0.3998357,      0.807369947,      4.04938,      1.820053,      4.37891245,      5.023925,      -0.983128667,      1.17288423,      5.36632156,      7.62595129,      9.731082,      2.16314888,      1.23764253,      -0.0597170033,      -1.16168725,      5.888235,      11.8514833,      16.01842,      5.0607686,      -1.02029264,      -2.673192,      -6.17305946,      -7.985189,      6.149627,      9.001264,      4.645575,      1.17826259,      1.88865864,      -3.40348649,      -1.73889589,      4.70277071,      6.62931156,      5.01530552,      -0.577166557    ],    \"ppeHead\": {      \"noHelmet\": 0.00126802735,      \"hasHelmet\": 0.99873203    },    \"nearness\": {      \"nearVehicle\": 0.0577901527,      \"notNearVehicle\": 0.94220984    },    \"insideness\": {      \"insideVehicle\": 0.03138556,      \"outsideVehicle\": 0.968614459    },    \"humanVerifier\": {      \"trash\": 0.0112218717,      \"fullBody\": 0.976276755,      \"partialBody\": 0.0125014838    },    \"humanClassifierMap\": [      -1.73970532,      -2.85918713,      -2.70079041,      -3.721581,      -1.81656981,      -2.44069362,      -1.40111458,      -4.000273,      -4.60808754,      1.10428786,      1.04392862,      0.304332048,      -2.06066585,      -2.39525819,      -3.3313694,      -1.846811,      15.4956436,      30.136673,      16.081913,      -2.22774482,      -4.52020741,      -4.340676,      -2.04640055,      34.03086,      53.6303749,      35.346096,      4.701389,      1.2251333,      3.29009318,      4.8522,      40.7625656,      51.1610336,      37.2630539,      6.11245728,      1.18080974,      5.98671865,      10.2589035,      20.7172928,      12.7433176,      7.092936,      0.7165336,      5.485745,      6.253988,      5.936791,      2.42576575,      -1.87106538,      -2.36334419,      -2.21449733,      5.7070756,      4.5434494,      4.31783867,      0.7231055,      -1.15942848,      0.660537064,      0.438921422,      0.5047026,      5.19464636,      5.42521,      -1.99348223,      -2.76337433,      -1.17487872,      0.184885591,      0.7456929,      4.23184252,      1.95489216,      -10.2583761,      -27.8042641,      -19.9875546,      -0.383776963,      0.5891904,      1.68696213,      1.01320755,      -21.39991,      -36.0393867,      -25.9648418,      -3.21446681,      -0.326263726,      0.212074235,      0.3767312,      -13.4719067,      -20.7897415,      -11.8116674,      1.02518511,      0.0774536058,      -1.45099628,      -2.54494,      2.05800629,      4.07776165,      7.399632,      9.327226,      2.37282658,      -3.27764225,      -2.03233647,      1.44225919,      9.544004,      6.75921059,      6.518242,      4.54312325,      -1.83554709,      -0.07017443,      0.046139203,      0.492516249,      0.702267766,      1.3818928,      -0.57650286,      -1.78837037,      0.1725772,      1.452906,      1.99549627,      1.61540806,      1.03481209,      0.1256979,      0.319331974,      3.68108559,      5.35919,      8.886392,      3.190619,      -0.2865458,      -0.152938247,      -1.08393371,      1.6432569,      -3.02689838,      0.770056,      -3.17553473,      1.98433888,      1.87556338,      -0.1443183,      -0.47273615,      -12.8025818,      -19.7397461,      -18.01143,      -5.628797,      0.4270491,      -0.6445038,      -0.724895954,      -9.518784,      -15.7923775,      -13.2564907,      -6.52411366,      -5.507003,      -0.550934851,      0.595257,      -1.45435393,      -4.990513,      -7.27880859,      -6.2682085,      -4.98914766    ]  },  \"detectionTimestamp\": \"2018-08-17T13:56:37.820Z\"}";
                        string payload = 
                            "{\"alertType\": PERSON_WITHOUT_HELMET, \"confidence\": 0.8,event: " + eventpayload + "}";
                        //var jo = JObject.Parse(payload);

                        var message = payload;
                        WriteLine($"Sending message: {message}");
                        await eventHubClient.SendAsync(new EventData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message))));
                    }
                    catch (EventHubsCommunicationException ehce)
                    {
                        WriteLine($"EventHubsCommunicationException: {ehce.Message}");
                    }
                    catch (EventHubsException ehe)
                    {
                        WriteLine($"EventHubsException: {ehe.Message}");
                    }
                    catch (Exception ex)
                    {
                        WriteLine($"{DateTime.Now} > Exception: {ex.Message}");
                    }

                    await Task.Delay(10);
                }
            }

            WriteLine($"{numMessagesToSend} messages sent.");
        }
        #endregion
        #region Azure Queue
        private static async Task MainStorageQueueAsync(string[] args)
        {
            WriteLine("Enter your Storage Queue connection string:");
            var StorageQueueConnectionString = ReadLine();
            while (StorageQueueConnectionString.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Storage Queue connection string:");
                StorageQueueConnectionString = ReadLine();
            }
            WriteLine("Enter your Storage Queue name:");
            var StorageQueueName = ReadLine();
            while (StorageQueueName.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Storage Queue name:");
                StorageQueueName = ReadLine();
            }
            WriteLine("Enter number of messages to add: ");
            int StorageMessagesToSend = 0;
            while (!int.TryParse(ReadLine(), out StorageMessagesToSend))
            {
                WriteLine("Try again, this value must be numeric.");
            }
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageQueueConnectionString);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            storageAccountQueue = queueClient.GetQueueReference(StorageQueueName);
            if (await storageAccountQueue.CreateIfNotExistsAsync())
            {
                WriteLine($"Queue '{storageAccountQueue.Name}' Created.");
            }
            else
            {
                WriteLine($"Queue '{storageAccountQueue.Name}' Exists.");
            }
            await SendMessagesToStorageAccount(StorageMessagesToSend);
        }
        private static async Task SendMessagesToStorageAccount(int numMessagesToSend)
        {
            for (var i = 0; i < numMessagesToSend; i++)
            {
                try
                {
                    var message = $"Message {i}";
                    WriteLine($"Sending message: {message}");
                    await storageAccountQueue.AddMessageAsync(new CloudQueueMessage(message));
                }
                catch (StorageException se)
                {
                    WriteLine($"StorageException: {se.Message}");
                }
                catch (Exception ex)
                {
                    WriteLine($"{DateTime.Now} > Exception: {ex.Message}");
                }

                await Task.Delay(10);
            }

            WriteLine($"{numMessagesToSend} messages sent.");
        }
        #endregion
        #region HTTP Trigger
        private static async Task MainHTTPTriggerAsync(string[] args)
        {
            WriteLine("Enter your HTTP Trigger URL:");
            var HTTPTriggerUrl = ReadLine();
            while (HTTPTriggerUrl.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your HTTP Trigger URL:");
                HTTPTriggerUrl = ReadLine();
            }
            WriteLine("Enter HTTP Trigger function key:");
            var HTTPTriggerFunctionKey = ReadLine();
            while (HTTPTriggerFunctionKey.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter HTTP Trigger function key:");
                HTTPTriggerFunctionKey = ReadLine();
            }
            WriteLine("Enter your name or any name:");
            var Name = ReadLine();
            while (Name.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter any value for Query String usage:");
                Name = ReadLine();
            }
            WriteLine("Enter number of requests to send: ");
            int HTTPRequestsToSend = 0;
            while (!int.TryParse(ReadLine(), out HTTPRequestsToSend))
            {
                WriteLine("Try again, this value must be numeric.");
            }
            await SendHTTPRequests(HTTPRequestsToSend, HTTPTriggerUrl, HTTPTriggerFunctionKey, Name);
        }
        private static async Task SendHTTPRequests(int numHTTPRequestsToSend, string Url, string FunctionKey, string Name)
        {
            for (var i = 0; i < numHTTPRequestsToSend; i++)
            {
                try
                {
                    var httpRequest = $"HTTP request {i}";
                    WriteLine($"Sending request: {httpRequest}");
                    var json = "{\"name\":\"" + Name + "\"}";
                    var encodedContent = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(Url + "?code=" + FunctionKey, encodedContent).ConfigureAwait(false);
                    WriteLine($"The response code is: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();
                    var resultContent = await response.Content.ReadAsStringAsync();
                    WriteLine(resultContent);
                }
                catch (HttpRequestException hre)
                {
                    WriteLine($"HttpRequestException: {hre.Message}");
                }
                catch (Exception ex)
                {
                    WriteLine($"{DateTime.Now} > Exception: {ex.Message}");
                }

                await Task.Delay(10);
            }

            WriteLine($"{numHTTPRequestsToSend} requests sent.");
        }
        #endregion
        #region Service Bus
        private static async Task MainServiceBusAsync(string[] args)
        {
            WriteLine("Enter your Service Bus connection string:");
            var ServiceBusConnectionString = ReadLine();
            while (ServiceBusConnectionString.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Service Bus connection string:");
                ServiceBusConnectionString = ReadLine();
            }
            WriteLine("Enter your Queue name:");
            var QueueName = ReadLine();
            while (QueueName.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter yourQueue name:");
                QueueName = ReadLine();
            }
            WriteLine("Enter number of messages to add: ");
            int ServiceBusMessagesToSend = 0;
            while (!int.TryParse(ReadLine(), out ServiceBusMessagesToSend))
            {
                WriteLine("Try again, this value must be numeric.");
            }
            queueClient = new QueueClient(ServiceBusConnectionString, QueueName);
            await SendMessagesToServicebus(ServiceBusMessagesToSend);
            await queueClient.CloseAsync();
        }
        private static async Task SendMessagesToServicebus(int numMessagesToSend)
        {
            for (var i = 0; i < numMessagesToSend; i++)
            {
                try
                {
                    var messageBody = $"Message-{i}-{Guid.NewGuid().ToString("N")}-{DateTime.Now.Minute}";
                    WriteLine($"Sending message: {messageBody}");
                    var message = new Message(Encoding.UTF8.GetBytes(messageBody));
                    await queueClient.SendAsync(message);
                }
                catch (ServiceBusTimeoutException sbte)
                {
                    WriteLine($"ServiceBusTimeoutException: {sbte.Message}");
                }
                catch (ServiceBusException sbe)
                {
                    WriteLine($"ServiceBusException: {sbe.Message}");
                }
                catch (Exception ex)
                {
                    WriteLine($"{DateTime.Now} > Exception: {ex.Message}");
                }

                await Task.Delay(10);
            }

            WriteLine($"{numMessagesToSend} messages sent.");
        }
        #endregion
        #region CosmosDB
        private static async Task MainCosmosDBAsync(string[] args)
        {
            WriteLine("Enter your Cosmos database name:");
            var CosmosDatabaseName = ReadLine();
            while (CosmosDatabaseName.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Cosmos database name:");
                CosmosDatabaseName = ReadLine();
            }
            WriteLine("Enter your Cosmos collection name:");
            var CosmosCollectionName = ReadLine();
            while (CosmosCollectionName.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Cosmos collection name:");
                CosmosCollectionName = ReadLine();
            }
            WriteLine("Enter your Cosmos endpoint url name:");
            var CosmosEndpointUrl = ReadLine();
            while (CosmosEndpointUrl.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Cosmos endpoint url name:");
                CosmosEndpointUrl = ReadLine();
            }
            WriteLine("Enter your Cosmos account key:");
            var CosmosAccountKey = ReadLine();
            while (CosmosAccountKey.Length == 0)
            {
                WriteLine("Try again, this value must have a length > 0");
                WriteLine("Enter your Cosmos account key:");
                CosmosAccountKey = ReadLine();
            }
            WriteLine("Enter number of documents to add: ");
            int CosmosDocumentsToSend = 0;
            while (!int.TryParse(ReadLine(), out CosmosDocumentsToSend))
            {
                WriteLine("Try again, this value must be numeric.");
            }

            try
            {
                using (documentClient = new DocumentClient(new Uri(CosmosEndpointUrl), CosmosAccountKey))
                {
                    Uri collectionUri = UriFactory.CreateDocumentCollectionUri(CosmosDatabaseName, CosmosCollectionName);
                    await SendDocumentsToCosmos(CosmosDocumentsToSend, collectionUri);
                    WriteLine("Press Y to delete these document.");
                    if (ReadLine() == "Y")
                    {
                        await DeleteCosmosDocuments(CosmosDocumentsToSend, CosmosDatabaseName, CosmosCollectionName);
                    }
                    else
                    {
                        WriteLine("No documents deleted.");
                    }
                }
            }
            catch (DocumentClientException dce)
            {
                WriteLine($"{dce.StatusCode} error occurred: {dce.Message}");
            }
            catch (Exception ex)
            {
                WriteLine($"Error occurred: {ex.Message}");
            }
        }
        private static async Task SendDocumentsToCosmos(int numDocumentsToSend, Uri collectionUri)
        {
            for (var i = 0; i < numDocumentsToSend; i++)
            {
                try
                {
                    var message = $"Document {i}";
                    CosmosDocument cosmosDocument = CreateCosmosDocument(i.ToString());
                    WriteLine($"Sending document: {message}");
                    await documentClient.CreateDocumentAsync(collectionUri, cosmosDocument);
                }
                catch (DocumentClientException dce)
                {
                    WriteLine($"{dce.StatusCode} error occurred: {dce.Message}");
                }
                catch (Exception ex)
                {
                    WriteLine($"Error occurred: {ex.Message}");
                }

                await Task.Delay(10);
            }

            WriteLine($"{numDocumentsToSend} documents sent.");
        }
        private static async Task DeleteCosmosDocuments(int numDocumentsToDelete, string cosmosDatabaseName, string cosmosCollectionName)
        {
            for (var i = 0; i < numDocumentsToDelete; i++)
            {
                try
                {
                    var message = $"Document {i}";
                    WriteLine($"Deleting document: {message}");
                    ResourceResponse<Document> response = await documentClient.DeleteDocumentAsync(
                            UriFactory.CreateDocumentUri(cosmosDatabaseName, cosmosCollectionName, i.ToString()));
                }
                catch (DocumentClientException dce)
                {
                    WriteLine($"{dce.StatusCode} error occurred: {dce.Message}");
                }
                catch (Exception ex)
                {
                    WriteLine($"Error occurred: {ex.Message}");
                }

                await Task.Delay(10);
            }

            WriteLine($"{numDocumentsToDelete} documents deleted.");
        }
        private static CosmosDocument CreateCosmosDocument(string documentId)
        {
            CosmosDocument cosmosDocument = new CosmosDocument()
            {
                Id = documentId,
                CreateDate = DateTime.Now,
                AccountNumber = $"Account{documentId}",
                Freight = 472.3108m,
                TotalDue = 985.018m,
                Items = new CosmosDocumentDetail[]
                {
                    new CosmosDocumentDetail
                    {
                        OrderQty = ToInt32(documentId),
                        ProductId = ToInt32(documentId) + ToInt32(documentId),
                        UnitPrice = 419.4589m
                    }
                },
            };
            return cosmosDocument;
        }
        public class CosmosDocument
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            public DateTime CreateDate { get; set; }
            public string AccountNumber { get; set; }
            public decimal Freight { get; set; }
            public decimal TotalDue { get; set; }
            public CosmosDocumentDetail[] Items { get; set; }
        }
        public class CosmosDocumentDetail
        {
            public int OrderQty { get; set; }
            public int ProductId { get; set; }
            public decimal UnitPrice { get; set; }
        }
        #endregion
    }
}
