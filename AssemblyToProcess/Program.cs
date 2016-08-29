using static System.Console;
namespace AssemblyToProcess
{
    public static class Program
    {
        static void Main( string[] args )
        {
            WriteLine( Add( 2, 4 ) );
            ReadLine();
        }

        private static int Add( int a, int b )
        {
            return a + b;
        }
    }
}
