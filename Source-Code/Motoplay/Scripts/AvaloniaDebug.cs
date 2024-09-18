using System;
using System.Diagnostics;

namespace Avalonia
{
    /*
    / This class makes debugging easier!
    / In Avalonia, using "Console.WriteLine()" to send logs does not make them visible within the Visual Studio Console,
    / unlike WPF, which uses "Console.WriteLine()" as the default debugging method. In Avalonia, they are only visible 
    / when calling "Debug.WriteLine()". However, on platforms such as Linux, logs issued by "Debug.WriteLine()" are not
    / visible in the Terminal that started the Avalonia App process. To solve this, this class provides a static method
    / to emit debug logs that are visible in any environment.
    */

    public class AvaloniaDebug
    {
        //Core methods

        public static void WriteLine(string message)
        {
            //Send the log
            Debug.WriteLine(message);
            Console.WriteLine(message);
        }
    }
}