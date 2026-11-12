// Only the tests' diagnostic logging needs Unity; production domain sources need no shim.
namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(object message) => System.Console.WriteLine(message);
    }
}
