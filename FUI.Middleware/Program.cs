using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace FUI.Middleware
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        /// <summary>
        /// Thread for all incoming messages -- typically new orders for WMS. Want these processed independently of outbound messages (ASNs).
        /// </summary>
        private static Thread _incomingMessageThread;

        /// <summary>
        /// Thread for all outgoing messages -- typically ASNs. Want these processed independently of inbound messages (new orders for WMS).
        /// </summary>
        private static Thread _outgoingMessageThread;


        private static bool _enableIncomingQueue;
        private static bool _enableOutgoingQueue;

        /// <summary>
        /// Main entry point to program. Queries for new messages in queue, and processes them based on the type of message.
        /// </summary>
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            Console.WriteLine("-------------------------------------------------------------------------------------------------");
            Console.WriteLine("--------- Starting to poll for new messages from queue. Press Ctrl + C to stop polling. ---------");
            Console.WriteLine("-------------------------------------------------------------------------------------------------");

            _enableIncomingQueue = Convert.ToBoolean(ConfigurationManager.AppSettings["enableIncomingQueue"]);
            _enableOutgoingQueue = Convert.ToBoolean(ConfigurationManager.AppSettings["enableOutgoingQueue"]);


            _incomingMessageThread = null;
            _outgoingMessageThread = null;

            bool runLoop = true;
            while (runLoop)
            {
                try
                {
                    RunThreads();
                }
                catch (Exception e)
                {
                    //Any uncaught exception means we aren't able to even start our processors at all, so need to abort
                    runLoop = false;
                    Log.Error("Error running processor threads", e);
                }
            }

            /* Code to get messages in dead letter queue
            string deadLetterPath = QueueClient.FormatDeadLetterPath(client.Path);
            QueueClient deadLetterClient = messaingFactory.CreateQueueClient(deadLetterPath);
             */
        }

        private static void RunThreads()
        {
            if (_incomingMessageThread == null && _enableIncomingQueue)
            {
                IncomingQueue incomingQueue = new IncomingQueue();
                _incomingMessageThread = new Thread(incomingQueue.Process);
                _incomingMessageThread.Start();
            }

            if (_outgoingMessageThread == null && _enableOutgoingQueue)
            {
                OutgoingQueue outgoingQueue = new OutgoingQueue();
                _outgoingMessageThread = new Thread(outgoingQueue.Process);
                _outgoingMessageThread.Start();
            }

            Thread.Sleep(5000);

            try
            {
                if (_incomingMessageThread != null)
                {
                    switch (_incomingMessageThread.ThreadState)
                    {
                        case ThreadState.Background:
                        case ThreadState.Running:
                        case ThreadState.WaitSleepJoin:
                            break;
                        default:
                            //Set to null to trigger object to be recreated
                            _incomingMessageThread = null;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                //Null object to cause it to be recreated
                _incomingMessageThread = null;
                Log.Error("Error checking thread state on incoming queue", e);
            }

            try
            {
                if (_outgoingMessageThread != null)
                {
                    switch (_outgoingMessageThread.ThreadState)
                    {
                        case ThreadState.Background:
                        case ThreadState.Running:
                        case ThreadState.WaitSleepJoin:
                            break;
                        default:
                            //Set to null to trigger object to be recreated
                            _outgoingMessageThread = null;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                //Null object to cause it to be recreated
                _outgoingMessageThread = null;
                Log.Error("Error checking thread state on outgoing queue", e);
            }

        }
    }
}
