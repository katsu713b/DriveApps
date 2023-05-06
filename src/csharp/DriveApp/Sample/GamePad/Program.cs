// See https://aka.ms/new-console-template for more information
using GamePad;
using System.Runtime.CompilerServices;


Console.WriteLine("Hello, World!");

var a = new RootCommand();
a.Run();

for (int i = 0; i < 10; i++)
{
    await Task.Delay(1000);
}

Console.WriteLine("Done");

Console.ReadLine();
