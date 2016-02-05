using System;
using System.Drawing;
using System.Reflection;

// The sole purpose of this C# project is to give the program an application icon.
// For some reason, this sometimes cuase UAC to kick in for an F# executable.
// See http://stackoverflow.com/questions/35182508/adding-a-resource-file-to-f-windows-app-causes-uac-on-app-start

namespace RepairVS2015Update
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var icon = Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().ManifestModule.Name);

            RepairVS2015UpdateLib.main(icon);
        }
    }
}

