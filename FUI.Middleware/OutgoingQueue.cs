using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DECK.Core.Common;
using log4net;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace FUI.Middleware
{
    /// <summary>
    /// Processes all messages that need to be delivered to the outgoing queue (ship confirmations for example)
    /// </summary>
    public class OutgoingQueue
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OutgoingQueue));
        private readonly string _asnApiEndpoint = ConfigurationManager.AppSettings["asnApiEndpoint"];
        private QueueClient _queueClient;


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
                string queueName = Enum.GetName(typeof(QueueType), QueueType.ToAdmin);
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
        /// Main processing loop for all outgoing messages. Checks system for new outgoing data (ASNs) and if
        /// found, sends that data to the queue.
        /// </summary>
        public void Process()
        {
            int sleepValue;
            if (!Int32.TryParse(ConfigurationManager.AppSettings["outgoingQueueSleepValue"], out sleepValue) || sleepValue < 1)
                sleepValue = 9000;

            ShipConfirmationProcessor shipConfirmationProcessor = new ShipConfirmationProcessor();

            CreateQueueClient();

            Log.Info("Starting outgoing queue process...");

            //Keep polling for new messages until program is terminated
            while (true)
            {
                IEnumerable<AsnOrder> shipConfirmations = shipConfirmationProcessor.GetNewShipConfirmations();
                foreach (AsnOrder asn in shipConfirmations)
                {
                    //Unique ID is added to message to help with tracking/debugging
                    string uniqueGuid = Guid.NewGuid().ToString();
                    bool queueError = false;

                    try
                    {
                        BrokeredMessage message = new BrokeredMessage(shipConfirmationProcessor.ConvertAsn(asn));
                        message.Properties["uniqueID"] = uniqueGuid;
                        message.Properties["asnApiEndpoint"] = _asnApiEndpoint;
                        message.Properties["messageType"] = (int)QueueMessageType.ShipConfirmationToOms;
                        Log.Info("Sending message for new ship confirmation (" + uniqueGuid + ")");
                        try
                        {
                            _queueClient.Send(message);
                        }
                        catch (Exception e)
                        {
                            Log.Error("Error sending message to queue", e);

                            //Record that we had a queue error, but throw so outer exception is caught
                            //which will cause ASN record to not get marked as processed
                            queueError = true;
                            throw;
                        }

                        //Mark record as processed now that it has been saved to queue. If any error occurs prior
                        //to this, record will be re-queued the next time this loop runs
                        shipConfirmationProcessor.AcknowledgeAsn(asn.IDInterfaceShipmentConfirmationHeader);
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error sending new ship confirmation to queue", e);
                    }

                    if (queueError)
                    {
                        //Sleep to try to let system/network recover, before trying to recreate queue client
                        Thread.Sleep(10000);

                        CreateQueueClient();
                    }
                }

                //Sleep in between iterations
                Thread.Sleep(sleepValue);
            }
        }
    }
}
