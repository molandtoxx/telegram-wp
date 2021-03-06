// 
// This is the source code of Telegram for Windows Phone v. 3.x.x.
// It is licensed under GNU GPL v. 2 or later.
// You should have received a copy of the license in this archive (see LICENSE).
// 
// Copyright Evgeny Nadymov, 2013-present.
// 
using System.Diagnostics;
using System.Windows;
using PhoneVoIPApp.BackEnd;
using PhoneVoIPApp.BackEnd.OutOfProcess;

namespace PhoneVoIPApp.Agents
{
    /// <summary>
    /// A static class that does process-level initialization/deinitializations.
    /// </summary>
    public static class AgentHost
    {
        #region Methods

        /// <summary>
        /// Indicates that an agent started running.
        /// </summary>
        internal static void OnAgentStarted()
        {
            // Initialize the native code - this only needs to be done once per process,s
            // but the method below will effectively be a no-op if called more than once.
            BackEnd.Globals.Instance.StartServer(RegistrationHelper.OutOfProcServerClassNames);
        }

        #endregion

        #region Private members

        /// <summary>
        /// Class constructor
        /// </summary>
        static AgentHost()
        {
            // Subscribe to the unhandled exception event
            Deployment.Current.Dispatcher.BeginInvoke(delegate
            {
                Application.Current.UnhandledException += AgentHost.OnUnhandledException;
            });

            // Create the singleton video renderer
            AgentHost.videoRenderer = new VideoRenderer();

            AgentHost.mtProtoUpdater = new MTProtoUpdater();

            // Store a pointer to the video renderer in the native Globals singleton,
            // so that the renderer can be used by native code in this process.
            Globals.Instance.VideoRenderer = AgentHost.videoRenderer;

            Globals.Instance.MTProtoUpdater = AgentHost.mtProtoUpdater;
        }

        /// <summary>
        /// Code to execute on unhandled exceptions.
        /// </summary>
        private static void OnUnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine("[AgentHost] An unhandled exception of type {0} has occurred. Error code: 0x{1:X8}. Message: {2}",
                e.ExceptionObject.GetType(), e.ExceptionObject.HResult, e.ExceptionObject.Message);

            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        #endregion

        #region Private

        // The singleton video renderer
        static VideoRenderer videoRenderer;

        static MTProtoUpdater mtProtoUpdater;

        #endregion
    }
}
