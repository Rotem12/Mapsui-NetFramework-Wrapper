using System;
using System.IO;
using SQLite;

class Program {
    static void Main() {
        try {
            using var conn = new SQLiteConnection(@"E:\Projects\GitHub\Mapsui48\israel-and-palestine-260720.osm.mbtiles");
            var res = conn.ExecuteScalar<byte[]>("SELECT tile_data FROM tiles LIMIT 1");
            Console.WriteLine(res[0] == 0x1F && res[1] == 0x8B ? "GZIPPED" : "UNCOMPRESSED");
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }
    }
}
