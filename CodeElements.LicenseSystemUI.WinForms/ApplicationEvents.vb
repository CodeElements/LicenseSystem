Imports Microsoft.VisualBasic.ApplicationServices

Namespace My
    ' The following events are available for MyApplication:
    ' Startup: Raised when the application starts, before the startup form is created.
    ' Shutdown: Raised after all application forms are closed.  This event is not raised if the application terminates abnormally.
    ' UnhandledException: Raised if the application encounters an unhandled exception.
    ' StartupNextInstance: Raised when launching a single-instance application and the application is already active. 
    ' NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.
    Partial Friend Class MyApplication
        Protected Overrides Function OnStartup(eventArgs As StartupEventArgs) As Boolean
            LicenseSystemUiService.Run(Guid.Parse("5bba05f5-0fd3-43eb-8468-e9dd5d6031a6"), "*****-*****-*****-*****-*****")

            Return MyBase.OnStartup(eventArgs)
        End Function
    End Class
End Namespace
