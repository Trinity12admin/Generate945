using System;
using System.Configuration;
using System.Threading;
using DECK.Core.Common;
using log4net;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace FUI.Middleware
{
    /// <summary>
    /// Processes all messages on the incoming queue -- typically new orders from OMS that need to be shipped by the warehouse
    /// </summary>
    public class IncomingQueue
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(IncomingQueue));
        private readonly NewOrderProcessor _newOrderProcessor;
        private readonly int _sleepValue;
        private QueueClient _queueClient;

        /// <summary>
        /// How many times do we try to reprocess a message before moving to dead letter queue
        /// </summary>
        private const int MaxRetryCount = 5;

        /// <summary>
        /// Constructor
        /// </summary>
        public IncomingQueue()
        {
            _newOrderProcessor = new NewOrderProcessor();

            int sleepValue;
            if (!Int32.TryParse(ConfigurationManager.AppSettings["incomingQueueSleepValue"], out sleepValue) || sleepValue < 1)
                _sleepValue = 5000;

            CreateQueueClient();
        }

        /// <summary>
        /// Perform actual work. This process will run continuously until it is terminated.
        /// </summary>
        public void Process()
        {
            //Keep polling for new messages until program is terminated
            while (true)
            {
                BrokeredMessage message;
                while ((message = GetNewMessageFromQueue()) != null)
                {
                    int retryCount = 0;
                    while (retryCount < MaxRetryCount)
                    {
                        if (ProcessNewMessage(message))
                            break;

                        retryCount++;
                    }

                    if (retryCount == MaxRetryCount)
                    {
                        Log.Info("Moving message to dead letter queue");
                        message.DeadLetter("UnableToProcess", "Error while trying to process new order");
                    }
                }

                //Sleep in between iterations
                Thread.Sleep(_sleepValue);
            }
        }

        /// <summary>
        /// Safely tries to create a new instance of queue client for Azure service bus. Called on class creation, but
        /// also called if an error is detected and we need to recreate the queue client connection.
        /// </summary>
        private void CreateQueueClient()
        {
            //Make sure any previous client reference is removed
            _queueClient = null;

            try
            {
                string queueName = Enum.GetName(typeof(QueueType), QueueType.ToWMS);
                string connectionString = ConfigurationManager.AppSettings["queueConnectionString"];

                var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);
                if (!namespaceManager.QueueExists(queueName))
                {
                    namespaceManager.CreateQueue(queueName);
                    Log.Error("Queue did not exist...creating: " + queueName);
                }

                MessagingFactory messagingFactory = MessagingFactory.CreateFromConnectionString(connectionString);

                // Initialize the connection to Service Bus Queue
                _queueClient = messagingFactory.CreateQueueClient(queueName);
            }
            catch (Exception e)
            {
                Log.Error("Error creating queue client", e);
            }
        }

        /// <summary>
        /// Safely try to get a new message from queue. If an exception is encountered, try to
        /// recreate the queue client connection, after sleeping first to allow the system to recover.
        /// If queue client is null, or still in an error state, we return a null message so the main
        /// Process loop can continue running -- it will cause us to keep checking the queue until it
        /// comes back on-line.
        /// </summary>
        /// <returns></returns>
        private BrokeredMessage GetNewMessageFromQueue()
        {
            BrokeredMessage message = null;
            bool error = false;
            try
            {
                message = _queueClient.Receive(TimeSpan.FromSeconds(10));
            }
            catch (Exception e)
            {
                Log.Error("Error getting new message from queue (will recreate client)", e);
                error = true;
            }

            //If error, try to recreate client
            if (error)
            {
                try
                {
                    //Try to correctly close queue client
                    _queueClient.Close();
                }
                catch (Exception e)
                {
                    Log.Error("Error closing queue client", e);
                }

                //Wait 10 seconds to try to give any network issues a chance to recover
                Thread.Sleep(10000);

                try
                {
                    CreateQueueClient();
                    message = _queueClient.Receive(TimeSpan.FromSeconds(10));
                }
                catch (Exception e)
                {
                    Log.Error("Error getting message from recreated queue client", e);
                }
            }

            return message;
        }

        /// <summary>
        /// Processes a new message and routes to correct logic based on message type
        /// </summary>
        /// <param name="message">Message from queue</param>
        /// <returns>Boolean indicating if we were able to successfully process this message</returns>
        private bool ProcessNewMessage(BrokeredMessage message)
        {
            //Unique ID from message to help with tracking and correlating logs between OMS and Middleware (required, so error if not in message)
            string uniqueId;
            QueueMessageType messageType;

            try
            {
                messageType = (QueueMessageType)message.Properties["messageType"];
                uniqueId = message.Properties["uniqueID"].ToString();
            }
            catch (Exception e)
            {
                Log.Error("Error extracting message properties", e);
                return false;
            }

            //These are only messages that are on the incoming queue (ship confirmations + returns are sent to outgoing queue)
            switch (messageType)
            {
                case QueueMessageType.ShipmentFromOms:
                    return _newOrderProcessor.SendOrderToWarehouse(message, uniqueId);
                default:
                    Log.Error("Unknown message type encountered");
                    return false;
            }
        }
    }
}
