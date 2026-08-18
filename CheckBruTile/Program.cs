using System;
using Mapsui.UI.WindowsForms;
class Program {
static void Main() {
try {
var map = new MapControl();
map.UseGPU = true;
Console.WriteLine("Success");
} catch (Exception ex) {
Console.WriteLine(ex);
}
}
}
