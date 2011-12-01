using System;

namespace HeatSite
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("HeatSite version: {0}", typeof (Program).Assembly.GetName().Version);
            var distiller = new SiteDistiller();
            if (!Parser.ParseArgumentsWithUsage(args, distiller))
            {
                Environment.Exit(2);
            }
            distiller.Execute();
        }
    }
}