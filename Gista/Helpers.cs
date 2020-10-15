using System;
using System.IO;
using System.Linq;

namespace Gista
{
    public static class Helpers
    {
        public static void ErrorExit(string message, int exitCode = 1)
        {
            Console.Error.WriteLine(message);
            Environment.Exit(exitCode);
        }

        public static void ErrorNotFound(string path)
        {
            if (path == null || !File.Exists(path))
                ErrorExit($"File '{path}' does not exist");
        }

        //https://stackoverflow.com/a/14655199/5688146
        public static string[] Splitz(this string str)
        {
            return str.Split('"')
                .Select((element, index) => index % 2 == 0 // If even index
                    ? element.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries) // Split the item
                    : new string[] {element}) // Keep the entire item
                .SelectMany(element => element).ToArray();
        }

        public static T Needed<T>(Func<T> f)
        {
            try
            {
                return f();
            }
            catch (Exception e)
            {
                ErrorExit(e.Message);
            }

            return default;
        }
    }
}